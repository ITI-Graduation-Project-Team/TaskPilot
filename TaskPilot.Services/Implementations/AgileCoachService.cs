//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using Microsoft.EntityFrameworkCore;
//using TaskPilot.AI.Agents;
//using TaskPilot.AI.Exceptions;
//using TaskPilot.Data.Repositories;
//using TaskPilot.DTOs.AI.AgileCoach;
//using TaskPilot.Models.Common.Errors;
//using TaskPilot.Models.Common.Results;
//using TaskPilot.Models.Entities;
//using TaskPilot.Services.Interfaces;

//namespace TaskPilot.Services.Implementations
//{
//    public class AgileCoachService : IAgileCoachService
//    {
//        private readonly IRepository<TaskItem> _taskRepository;
//        private readonly IRepository<TaskAiSummary> _summaryRepository;
//        private readonly ICurrentUserService _currentUserService;
//        private readonly AgileCoachAgent _agileCoachAgent;

//        public AgileCoachService(
//            IRepository<TaskItem> taskRepository,
//            IRepository<TaskAiSummary> summaryRepository,
//            ICurrentUserService currentUserService,
//            AgileCoachAgent agileCoachAgent)
//        {
//            _taskRepository = taskRepository;
//            _summaryRepository = summaryRepository;
//            _currentUserService = currentUserService;
//            _agileCoachAgent = agileCoachAgent;
//        }

//        private async Task<Result<TaskItem>> GetAndValidateTaskOwnershipAsync(Guid taskId, CancellationToken cancellationToken)
//        {
//            var task = await _taskRepository.GetQueryable()
//                .Include(t => t.UserStory)
//                    .ThenInclude(us => us.Project)
//                        .ThenInclude(p => p.ProjectEmployees)
//                .Include(t => t.AiSummary)
//                .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

//            if (task == null)
//            {
//                return Result.Failure<TaskItem>(CommonErrors.NotFound("TaskItem"));
//            }

//            var project = task.UserStory?.Project;
//            if (project == null)
//            {
//                return Result.Failure<TaskItem>(CommonErrors.InvalidInput("Task is not associated with a project."));
//            }

//            var currentUserId = _currentUserService.UserId;
//            if (currentUserId == null)
//            {
//                return Result.Failure<TaskItem>(CommonErrors.Unauthorized());
//            }

//            bool hasAccess = project.ManagerId == currentUserId || project.ProjectEmployees.Any(pe => pe.EmployeeId == currentUserId);

//            if (!hasAccess)
//            {
//                return Result.Failure<TaskItem>(CommonErrors.Forbidden("You do not have access to this project."));
//            }

//            if (project.RequirementsSessionId == null)
//            {
//                return Result.Failure<TaskItem>(CommonErrors.InvalidInput("Project requirements session ID is missing. Cannot retrieve context."));
//            }

//            return Result.Success(task);
//        }

//        public async Task<Result<AgileCoachSummaryServiceResult>> GetOrGenerateSummaryAsync(Guid taskId, string lang, CancellationToken cancellationToken = default)
//        {
//            var taskResult = await GetAndValidateTaskOwnershipAsync(taskId, cancellationToken);
//            if (!taskResult.IsSuccess) return Result.Failure<AgileCoachSummaryServiceResult>(taskResult.Error!);

//            var task = taskResult.Value!;
            
//            if (task.AiSummary != null)
//            {
//                bool hasContentForLang = lang == "ar" ? !string.IsNullOrEmpty(task.AiSummary.ContentAr) : !string.IsNullOrEmpty(task.AiSummary.ContentEn);
                
//                if (hasContentForLang)
//                {
//                    var responseSummary = MapToResponse(task.AiSummary, lang);
//                    responseSummary.IsNewlyGenerated = false;
//                    return Result.Success(new AgileCoachSummaryServiceResult
//                    {
//                        Summary = responseSummary
//                    });
//                }
//            }

//            return await GenerateSummaryInternalAsync(task, lang, cancellationToken);
//        }

//        public async Task<Result<AgileCoachSummaryServiceResult>> RegenerateSummaryAsync(Guid taskId, string lang, CancellationToken cancellationToken = default)
//        {
//            var taskResult = await GetAndValidateTaskOwnershipAsync(taskId, cancellationToken);
//            if (!taskResult.IsSuccess) return Result.Failure<AgileCoachSummaryServiceResult>(taskResult.Error!);

//            var task = taskResult.Value!;
//            return await GenerateSummaryInternalAsync(task, lang, cancellationToken);
//        }

//        private async Task<Result<AgileCoachSummaryServiceResult>> GenerateSummaryInternalAsync(TaskItem task, string lang, CancellationToken cancellationToken)
//        {
//            try
//            {
//                var aiResult = await _agileCoachAgent.GenerateSummaryAsync(
//                    lang == "ar" ? task.TitleAr : task.TitleEn,
//                    (lang == "ar" ? task.DescriptionAr : task.DescriptionEn) ?? string.Empty,
//                    task.UserStory!.Project.RequirementsSessionId!.Value,
//                    lang);

//                var summary = task.AiSummary ?? new TaskAiSummary
//                {
//                    TaskItemId = task.Id,
//                    GeneratedAt = DateTime.UtcNow
//                };

//                var structuredContent = JsonSerializer.Serialize(new
//                {
//                    codebaseNotes = aiResult.CodebaseNotes,
//                    relatedPastTasks = aiResult.RelatedPastTasks,
//                    techStackContext = aiResult.TechStackContext,
//                    suggestedImplementationGuidance = aiResult.SuggestedImplementationGuidance
//                });

//                if (lang == "ar")
//                {
//                    summary.ContentAr = structuredContent;
//                }
//                else
//                {
//                    summary.ContentEn = structuredContent;
//                }

//                summary.CitationsJson = JsonSerializer.Serialize(aiResult.Citations);
//                summary.GeneratedAt = DateTime.UtcNow;

//                if (task.AiSummary == null)
//                {
//                    await _summaryRepository.AddAsync(summary);
//                    task.AiSummary = summary;
//                }
//                else
//                {
//                    _summaryRepository.Update(summary);
//                }

//                // NOTE: We do not call SaveChangesAsync here. The Controller will do it.

//                var responseSummary = MapToResponse(summary, lang);
//                responseSummary.IsNewlyGenerated = true;

//                return Result.Success(new AgileCoachSummaryServiceResult
//                {
//                    Summary = responseSummary
//                });
//            }
//            catch (AgileCoachException ex)
//            {
//                if (ex.Message.Contains("No relevant context found"))
//                {
//                    return Result.Failure<AgileCoachSummaryServiceResult>(AgileCoachErrors.KnowledgeBaseEmpty(ex.Message));
//                }
//                return Result.Failure<AgileCoachSummaryServiceResult>(AgileCoachErrors.SummaryGenerationFailed(ex.Message));
//            }
//            catch (Exception ex)
//            {
//                return Result.Failure<AgileCoachSummaryServiceResult>(AgileCoachErrors.SummaryGenerationFailed(ex.Message));
//            }
//        }

//        public async IAsyncEnumerable<string> StreamChatAsync(Guid taskId, string userMessage, List<ChatMessageDto> history, string lang)
//        {
//            var taskResult = await GetAndValidateTaskOwnershipAsync(taskId, CancellationToken.None);
//            if (!taskResult.IsSuccess)
//            {
//                // The controller SSE loop detects chunks starting with "__ERROR__:"
//                // and writes them as a terminal error event: "event: error\ndata: {code}\n\n"
//                yield return $"__ERROR__:{taskResult.Error!.Code}";
//                yield break;
//            }

//            var task = taskResult.Value!;
//            var sessionId = task.UserStory!.Project.RequirementsSessionId!.Value;

//            IAsyncEnumerable<string>? stream = null;
//            string? error = null;
//            try
//            {
//                stream = _agileCoachAgent.StreamChatAsync(userMessage, history, sessionId, lang);
//            }
//            catch (Exception ex)
//            {
//                error = $"__ERROR__:{ex.Message}";
//            }

//            if (error != null)
//            {
//                yield return error;
//                yield break;
//            }

//            IAsyncEnumerator<string>? enumerator = null;
//            try
//            {
//                enumerator = stream!.GetAsyncEnumerator();
//            }
//            catch (Exception ex)
//            {
//                error = $"__ERROR__:{ex.Message}";
//            }

//            if (error != null)
//            {
//                yield return error;
//                yield break;
//            }
            
//            while (true)
//            {
//                string? chunk = null;
//                try
//                {
//                    if (!await enumerator!.MoveNextAsync())
//                    {
//                        break;
//                    }
//                    chunk = enumerator.Current;
//                }
//                catch (Exception ex)
//                {
//                    error = $"__ERROR__:{ex.Message}";
//                    break;
//                }
                
//                if (error != null)
//                {
//                    yield return error;
//                    break;
//                }
                
//                if (chunk != null)
//                {
//                    yield return chunk;
//                }
//            }
//        }

//        private AgileCoachSummaryResponse MapToResponse(TaskAiSummary summary, string lang)
//        {
//            var content = lang == "ar" ? summary.ContentAr : summary.ContentEn;
//            AgileCoachContentDto? parsed = null;
//            if (!string.IsNullOrEmpty(content))
//            {
//                parsed = JsonSerializer.Deserialize<AgileCoachContentDto>(
//                    content,
//                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
//            }

//            var citations = string.IsNullOrEmpty(summary.CitationsJson)
//                ? new List<CitationDto>()
//                : JsonSerializer.Deserialize<List<CitationDto>>(
//                      summary.CitationsJson,
//                      new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
//                  ?? new List<CitationDto>();

//            return new AgileCoachSummaryResponse
//            {
//                Id = summary.Id,
//                TaskItemId = summary.TaskItemId,
//                CodebaseNotes = parsed?.CodebaseNotes ?? string.Empty,
//                RelatedPastTasks = parsed?.RelatedPastTasks ?? string.Empty,
//                TechStackContext = parsed?.TechStackContext ?? string.Empty,
//                SuggestedImplementationGuidance = parsed?.SuggestedImplementationGuidance ?? string.Empty,
//                Citations = citations,
//                GeneratedAt = summary.GeneratedAt
//            };
//        }
//    }
//}
