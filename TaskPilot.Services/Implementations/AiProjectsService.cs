using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Extensions;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.AI;
using TaskPilot.DTOs.Chat;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Services.Implementations
{
    public class AiProjectsService : IAiProjectsService
    {
        private readonly IEnumerable<IDocumentTextExtractor> _extractors;
        private readonly IAiKernelService _kernelService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IProjectChatSessionRepository _chatSessionRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly ILogger<AiProjectsService> _logger;
        private readonly IProjectChatService _chatService;
        private readonly TaskPilot.AI.Agents.Planning.WBSGenerationAgent _wbsGenerationAgent;
        private readonly IWbsPersistenceService _wbsPersistenceService;
        private readonly ITemporaryBrdStore _tempBrdStore;
        private readonly ICurrentUserService _currentUserService;
        private readonly IRepository<User> _userRepository;
        private readonly IFileValidatorService _fileValidator;

        private static readonly string[] RequiredCategories = new[]
        {
            "ProjectName", "Domain", "TargetUsers", "CoreFeatures",
            "TechConstraints", "Timeline", "TeamSize", "DefinitionOfDone"
        };

        public AiProjectsService(
            IEnumerable<IDocumentTextExtractor> extractors,
            IAiKernelService kernelService,
            IUnitOfWork unitOfWork,
            IProjectChatSessionRepository chatSessionRepository,
            IRepository<Project> projectRepository,
            ILogger<AiProjectsService> logger,
            IProjectChatService chatService,
            TaskPilot.AI.Agents.Planning.WBSGenerationAgent wbsGenerationAgent,
            IWbsPersistenceService wbsPersistenceService,
            ITemporaryBrdStore tempBrdStore,
            ICurrentUserService currentUserService,
            IRepository<User> userRepository,
            IFileValidatorService fileValidator)
        {
            _extractors = extractors;
            _kernelService = kernelService;
            _unitOfWork = unitOfWork;
            _chatSessionRepository = chatSessionRepository;
            _projectRepository = projectRepository;
            _logger = logger;
            _chatService = chatService;
            _wbsGenerationAgent = wbsGenerationAgent;
            _wbsPersistenceService = wbsPersistenceService;
            _tempBrdStore = tempBrdStore;
            _currentUserService = currentUserService;
            _userRepository = userRepository;
            _fileValidator = fileValidator;
        }

        public async Task<Result<BrdUploadResultDto>> UploadBrdAsync(IFormFile file, Guid? projectId, CancellationToken cancellationToken = default)
        {
            try
            {
                var validationResult = await _fileValidator.ValidateAsync(
                    file,
                    new[] { FileType.Pdf, FileType.Docx, FileType.Txt },
                    15 * 1024 * 1024, // 15MB limit
                    cancellationToken);

                if (!validationResult.IsSuccess)
                    return Result.Failure<BrdUploadResultDto>(validationResult.Error!);

                var extractor = _extractors.FirstOrDefault(e => e.CanHandle(file.ContentType, file.FileName));
                if (extractor == null)
                    return Result.Failure<BrdUploadResultDto>(new Error("UnsupportedFile", ErrorType.Validation, "File format is not supported."));

                string extractedText;
                using (var stream = file.OpenReadStream())
                {
                    extractedText = await extractor.ExtractTextAsync(stream, cancellationToken);
                }

                if (!projectId.HasValue)
                {
                    var userId = _currentUserService.UserId;
                    if (userId == null)
                        return Result.Failure<BrdUploadResultDto>(new Error("Unauthorized", ErrorType.Unauthorized, "User is not authenticated."));
                    
                    var user = await _userRepository.GetByIdAsync(userId.Value);
                    if (user == null)
                        return Result.Failure<BrdUploadResultDto>(CommonErrors.NotFound("User"));

                    var newProject = new Project
                    {
                        NameEn = $"Draft Project {Guid.NewGuid().ToString().Substring(0, 8)}",
                        DescriptionEn = "Generated by AI",
                        Status = TaskPilot.Models.Enums.ProjectStatus.Draft,
                        ManagerId = userId.Value,
                        // TODO: CompanyId must never be Guid.Empty in production.
                        // User registration flow must enforce CompanyId as required.
                        // This was identified during E2E testing — track as 
                        // data integrity issue.
                        CompanyId = user.CompanyId ?? Guid.Empty
                    };
                    await _projectRepository.AddAsync(newProject);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    projectId = newProject.Id;
                }
                else
                {
                    var existingProject = await _projectRepository.GetByIdAsync(projectId.Value);
                    if (existingProject == null)
                        return Result.Failure<BrdUploadResultDto>(CommonErrors.NotFound("Project"));
                }

                try
                {
                    // Ensure the session exists
                    var sessionResult = await _chatService.GetOrCreateSessionAsync(projectId.Value, cancellationToken);
                    if (sessionResult.IsSuccess)
                    {
                        // Fetch the actual entity
                        var sessionEntity = await _chatSessionRepository.GetByProjectIdAsync(projectId.Value, cancellationToken);
                        if (sessionEntity != null)
                        {
                            sessionEntity.BrdExtractedText = extractedText;
                            _chatSessionRepository.Update(sessionEntity);
                            await _unitOfWork.SaveChangesAsync(cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to persist BRD text to project chat session.");
                }
                
                // Also store in temporary store regardless of DB session success
                _tempBrdStore.Store(projectId.Value, extractedText);

                // AI Gap Analysis
                var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
                var chatService = kernel.GetRequiredService<IChatCompletionService>();

                var prompt = $@"
You are a project management AI. Analyze the following Business Requirement Document (BRD) and identify which of these required categories are MISSING or AMBIGUOUS:
{string.Join(", ", RequiredCategories)}

Return your analysis as a JSON object with a single array property 'detectedGaps' containing the exact names of the missing categories.

BRD Content:
{extractedText}";

                var aiResponse = await chatService.GetChatMessageContentWithTelemetryAsync(prompt, null, kernel, cancellationToken);
                var aiContent = aiResponse.Content ?? "{}";
                
                // Extract json from possible markdown
                if (aiContent.Contains("```json"))
                {
                    var start = aiContent.IndexOf("```json") + 7;
                    var end = aiContent.LastIndexOf("```");
                    aiContent = aiContent.Substring(start, end - start);
                }

                List<string> gaps = new List<string>();
                try
                {
                    using var doc = JsonDocument.Parse(aiContent);
                    if (doc.RootElement.TryGetProperty("detectedGaps", out var gapsProp))
                    {
                        foreach (var gap in gapsProp.EnumerateArray())
                        {
                            gaps.Add(gap.GetString() ?? "");
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JSON from AI response.");
                }

                var answered = RequiredCategories.Except(gaps).ToList();
                int completenessScore = (int)Math.Round((double)answered.Count / RequiredCategories.Length * 100);

                return Result.Success(new BrdUploadResultDto
                {
                    ProjectId = projectId.Value,
                    ExtractedText = extractedText,
                    DetectedGaps = gaps,
                    CompletenessScore = completenessScore
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BRD upload.");
                return Result.Failure<BrdUploadResultDto>(new Error("ServerError", ErrorType.Failure, ex.Message));
            }
        }

        public async Task<Result<AiChatResponseDto>> ProcessChatAsync(SendAiMessageDto request, CancellationToken cancellationToken = default)
        {
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory();
            var systemPrompt = $@"
You are a project management assistant helping a PM define a new project. 
The required categories are:
{string.Join(", ", RequiredCategories)}

You MUST systematically ask the user for information to fill these categories.
Identify the FIRST category from the list that has NOT been answered yet, and ask the user exactly ONE question about that specific category. DO NOT ask about categories that have already been answered. DO NOT ask multiple questions at once.
If the user provides information, acknowledge it briefly and move on to the next unanswered category.

Output your response in JSON format exactly like this:
{{
   ""message"": ""Your text response to the user"",
   ""answeredCategories"": [""list"", ""of"", ""answered"", ""categories"", ""so"", ""far""]
}}
";
            history.AddSystemMessage(systemPrompt);

            foreach (var msg in request.ChatHistory)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    history.AddUserMessage(msg.Content);
                else
                    history.AddAssistantMessage(msg.Content);
            }
            history.AddUserMessage(request.Message);

            var aiResponse = await chatService.GetChatMessageContentWithTelemetryAsync(history, null, kernel, cancellationToken);
            var aiContent = aiResponse.Content ?? "{}";

            if (aiContent.Contains("```json"))
            {
                var start = aiContent.IndexOf("```json") + 7;
                var end = aiContent.LastIndexOf("```");
                aiContent = aiContent.Substring(start, end - start);
            }

            string replyMessage = "";
            List<string> answered = new List<string>();

            try
            {
                using var doc = JsonDocument.Parse(aiContent);
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    replyMessage = msgProp.GetString() ?? "";
                
                if (doc.RootElement.TryGetProperty("answeredCategories", out var ansProp))
                {
                    foreach (var ans in ansProp.EnumerateArray())
                    {
                        var val = ans.GetString() ?? "";
                        if (RequiredCategories.Contains(val))
                            answered.Add(val);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON from AI chat response.");
                replyMessage = aiResponse.Content ?? "";
            }

            // Calculate score
            int score = (int)Math.Round((double)answered.Count / RequiredCategories.Length * 100);

            return Result.Success(new AiChatResponseDto
            {
                Message = replyMessage,
                CompletenessScore = score,
                AnsweredQuestions = answered,
                IsReadyToGenerate = score >= 85
            });
        }

        public async Task<Result<GenerateProjectDto>> GenerateProjectAsync(Guid? projectId, string projectName, CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
            {
                projectId = null;
            }

            // Note: Since generating a full project is complex, this logic constructs a minimal Project structure
            // and saves it inside a single transaction.

            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                Project? project;
                if (projectId.HasValue)
                {
                    // Existing project: retrieve it
                    project = await _projectRepository.GetByIdAsync(projectId.Value, p => p.Sprints);
                    if (project == null)
                        return Result.Failure<GenerateProjectDto>(CommonErrors.NotFound("Project"));
                    
                    // Clear existing sprints/stories/tasks for the project so we can replace them with new ones
                    // In a real app, you would use proper repositories to delete the cascade or rely on EF Core.
                    // Assuming EF Core tracks the collection and removing them works, or we can just clear the collection.
                    project.Sprints.Clear();
                }
                else
                {
                    // New project: create it
                    var userId = _currentUserService.UserId;
                    if (userId == null)
                        return Result.Failure<GenerateProjectDto>(new Error("Unauthorized", ErrorType.Unauthorized, "User is not authenticated."));
                        
                    var user = await _userRepository.GetByIdAsync(userId.Value);
                    if (user == null)
                        return Result.Failure<GenerateProjectDto>(CommonErrors.NotFound("User"));

                    var normalizedProjectName = projectName.Trim().ToUpper();
                    var nameExists = await _projectRepository.AnyAsync(candidate =>
                        candidate.CompanyId == (user.CompanyId ?? Guid.Empty) &&
                        candidate.NameEn.Trim().ToUpper() == normalizedProjectName);

                    if (nameExists)
                        return Result.Failure<GenerateProjectDto>(ProjectErrors.NameAlreadyExists);

                    project = new Project
                    {
                        NameEn = projectName.Trim(),
                        DescriptionEn = "Generated by AI",
                        Status = TaskPilot.Models.Enums.ProjectStatus.Draft,
                        ManagerId = userId.Value,
                        // TODO: CompanyId must never be Guid.Empty in production.
                        // User registration flow must enforce CompanyId as required.
                        // This was identified during E2E testing — track as 
                        // data integrity issue.
                        CompanyId = user.CompanyId ?? Guid.Empty
                    };
                }

                if (!projectId.HasValue)
                {
                    await _projectRepository.AddAsync(project);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                // 3a. Load BRD context
                string brdText = string.Empty;
                Guid chatSessionId = Guid.Empty;
                
                var sessionResult = await _chatService.GetOrCreateSessionAsync(project.Id, cancellationToken);
                if (sessionResult.IsSuccess)
                {
                    var session = sessionResult.Value;
                    brdText = session.BrdExtractedText ?? string.Empty;
                    chatSessionId = session.Id;
                }
                
                if (string.IsNullOrEmpty(brdText) && projectId.HasValue)
                {
                    brdText = _tempBrdStore.Retrieve(projectId.Value) ?? string.Empty;
                }

                // 3b. Build RequirementsSnapshot
                var snapshot = new RequirementsSnapshot
                {
                    BusinessRequirements = new List<string> { string.IsNullOrWhiteSpace(brdText) ? "Not specified" : brdText },
                    TechnicalRequirements = new List<string> { "Not specified" },
                    Constraints = new List<string> { "Not specified" },
                    Integrations = new List<string> { "Not specified" },
                    ScaleRequirements = new List<string> { "Not specified" }
                };

                // 3c. Invoke WBSGenerationAgent
                TaskPilot.AI.Models.Planning.GeneratedWbs generatedWbs;
                try
                {
                    generatedWbs = await _wbsGenerationAgent.GenerateAsync(
                        snapshot,
                        new List<string>(),
                        new List<string>(),
                        "Not specified",
                        new List<string>(),
                        chatSessionId,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogError(ex, "Failed to generate WBS from AI agent.");
                    return Result.Failure<GenerateProjectDto>(new Error("WbsGenerationFailed", ErrorType.Failure, ex.Message));
                }

                // 3d. Persist via WbsPersistenceService
                var persistenceResult = await _wbsPersistenceService.PersistAsync(project.Id, generatedWbs, cancellationToken);
                if (!persistenceResult.IsSuccess)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure<GenerateProjectDto>(persistenceResult.Error);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Success(new GenerateProjectDto
                {
                    ProjectId = project.Id,
                    ProjectName = project.NameEn
                });
            }
            catch (DbUpdateException ex) when (ProjectDuplicateNameDetector.IsDuplicateNameViolation(ex))
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<GenerateProjectDto>(ProjectErrors.NameAlreadyExists);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogError(ex, "Error generating project.");
                return Result.Failure<GenerateProjectDto>(new Error("GenerationFailed", ErrorType.Failure, ex.Message));
            }
        }

        public async Task<Result<ProjectChatHistoryDto>> GetChatHistoryAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var session = await _chatSessionRepository.GetByProjectIdWithMessagesAsync(projectId, cancellationToken);
            if (session == null)
            {
                return Result.Success(new ProjectChatHistoryDto
                {
                    ProjectId = projectId,
                    Messages = new List<AiChatMessageDto>()
                });
            }

            return Result.Success(new ProjectChatHistoryDto
            {
                ProjectId = projectId,
                Messages = session.Messages.Select(m => new AiChatMessageDto
                {
                    Role = m.Role,
                    Content = m.Content,
                    SequenceIndex = m.SequenceIndex,
                    Timestamp = m.Timestamp
                }).ToList()
            });
        }

        public async Task<Result<AiChatResponseDto>> ProcessFollowUpChatAsync(Guid projectId, string message, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null) return Result.Failure<AiChatResponseDto>(CommonErrors.NotFound("Project"));

            var session = await _chatSessionRepository.GetByProjectIdWithMessagesAsync(projectId, cancellationToken);

            // 1. Process with AI
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            var history = new ChatHistory("You are a helpful project management AI editing an existing project backlog.");
            if (session != null)
            {
                foreach (var msg in session.Messages.OrderBy(m => m.SequenceIndex))
                {
                    if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase)) history.AddUserMessage(msg.Content);
                    else history.AddAssistantMessage(msg.Content);
                }
            }
            history.AddUserMessage(message);

            var aiResponse = await chatService.GetChatMessageContentWithTelemetryAsync(history, null, kernel, cancellationToken);
            var reply = aiResponse.Content ?? "I have updated the backlog.";

            // 2. Save messages atomically via IProjectChatService
            var persistResult = await _chatService.AppendMessagesAsync(
                projectId,
                new List<(string, string)>
                {
                    ("User", message),
                    ("Assistant", reply)
                },
                cancellationToken);

            if (!persistResult.IsSuccess)
            {
                _logger.LogError("Failed to persist chat messages for project {ProjectId}: {Error}", projectId, persistResult.Error.Description);
            }

            return Result.Success(new AiChatResponseDto
            {
                Message = reply,
                CompletenessScore = 100, // already generated
                IsReadyToGenerate = true
            });
        }
    }
}
