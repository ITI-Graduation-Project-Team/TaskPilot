using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.ProjectPolicies;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Extensions;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;
using TaskPilot.Models.Common;

namespace TaskPilot.Services
{
    public class ProjectPolicyService : IProjectPolicyService
    {
        private readonly IEnumerable<IDocumentTextExtractor> _extractors;
        private readonly DocumentCategorizationAgent _categorizationAgent;
        private readonly ChunkingAgent _chunkingAgent;
        private readonly IVectorStore _vectorStore;
        private readonly KnowledgeOrchestrator _knowledgeOrchestrator;
        private readonly IRepository<Policy> _policyRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IFileStorageService _fileStorage;
        private readonly IFileValidatorService _fileValidator;
        private readonly ILogger<ProjectPolicyService> _logger;
        private readonly ILocalizationService _localizationService;

        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> _tenantLocks = new();

        public ProjectPolicyService(
            IEnumerable<IDocumentTextExtractor> extractors,
            DocumentCategorizationAgent categorizationAgent,
            ChunkingAgent chunkingAgent,
            IVectorStore vectorStore,
            KnowledgeOrchestrator knowledgeOrchestrator,
            IRepository<Policy> policyRepository,
            IRepository<Project> projectRepository,
            IFileStorageService fileStorage,
            IFileValidatorService fileValidator,
            ILogger<ProjectPolicyService> logger,
            ILocalizationService localizationService)
        {
            _extractors = extractors;
            _categorizationAgent = categorizationAgent;
            _chunkingAgent = chunkingAgent;
            _vectorStore = vectorStore;
            _knowledgeOrchestrator = knowledgeOrchestrator;
            _policyRepository = policyRepository;
            _projectRepository = projectRepository;
            _fileStorage = fileStorage;
            _fileValidator = fileValidator;
            _logger = logger;
            _localizationService = localizationService;
        }

        public async Task<Result<UploadProjectPolicyResponse>> IngestAsync(
            IngestProjectPolicyRequest request,
            Func<CancellationToken, Task> saveChangesAsync,
            CancellationToken cancellationToken = default)
        {
            if ((request.ProjectId.HasValue && request.RequirementSessionId.HasValue) ||
                (!request.ProjectId.HasValue && !request.RequirementSessionId.HasValue))
            {
                return Result.Failure<UploadProjectPolicyResponse>(
                    request.ProjectId.HasValue ? KnowledgeErrors.AmbiguousTenantIdentifier : KnowledgeErrors.MissingProjectPolicyIdentifier);
            }

            if (request.ProjectId.HasValue)
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value);
                if (project == null)
                {
                    return Result.Failure<UploadProjectPolicyResponse>(ProjectErrors.NotFound);
                }
            }

            if (request.File == null || request.File.Length == 0)
            {
                return Result.Failure<UploadProjectPolicyResponse>(CommonErrors.InvalidInput("File cannot be empty."));
            }

            string fileName = request.TitleEn ?? request.File.FileName;
            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(request.File.ContentType, request.File.FileName));
            if (extractor == null)
            {
                return Result.Failure<UploadProjectPolicyResponse>(CommonErrors.InvalidInput($"Unsupported file type: {request.File.ContentType} ({request.File.FileName})"));
            }

            string extractedText;
            using (var stream = request.File.OpenReadStream())
            {
                extractedText = await extractor.ExtractTextAsync(stream, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Result.Failure<UploadProjectPolicyResponse>(CommonErrors.InvalidInput("Extracted text is empty."));
            }

            Guid tenantId = request.ProjectId ?? request.RequirementSessionId!.Value;

            using var md5Doc = System.Security.Cryptography.MD5.Create();
            var hashInput = $"{tenantId}_{extractedText}";
            var documentId = new Guid(md5Doc.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashInput)));

            var semaphore = _tenantLocks.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var policyExists = await _policyRepository.FindSingleAsync(p => p.DocumentId == documentId || p.DocumentPublicId == documentId.ToString());
                if (policyExists != null)
                {
                    _logger.LogInformation("Project Policy document {FileName} already exists for Tenant {TenantId} with DocumentId {DocumentId}. Skipping ingestion.", fileName, tenantId, documentId);
                    return Result.Success(new UploadProjectPolicyResponse
                    {
                        DocumentId = documentId,
                        Message = "Document already ingested.",
                        ChunksCreated = 0
                    });
                }

                string? documentUrl = request.DocumentUrl;
                string? cloudinaryPublicId = request.CloudinaryPublicId;

                if (string.IsNullOrEmpty(documentUrl) && !request.SkipCloudUpload)
                {
                    string path = request.ProjectId.HasValue 
                        ? $"taskpilot/project-policies/projects/{request.ProjectId.Value}" 
                        : $"taskpilot/project-policies/sessions/{request.RequirementSessionId!.Value}";
                        
                    var validationResult = await _fileValidator.ValidateAsync(
                        request.File,
                        new[] { FileType.Pdf, FileType.Docx, FileType.Txt },
                        15 * 1024 * 1024,
                        cancellationToken);
                        
                    if (!validationResult.IsSuccess)
                    {
                        return Result.Failure<UploadProjectPolicyResponse>(validationResult.Error!);
                    }

                    var uploadResult = await _fileStorage.UploadFileAsync(request.File, path);
                    if (!uploadResult.IsSuccess)
                    {
                        return Result.Failure<UploadProjectPolicyResponse>(uploadResult.Error!);
                    }
                    documentUrl = uploadResult.Value.Url;
                    cloudinaryPublicId = uploadResult.Value.PublicId;
                }

                var category = await _categorizationAgent.CategorizeAsync(fileName, extractedText, cancellationToken);
                var chunks = await _chunkingAgent.ChunkContentAsync(documentId, extractedText, cancellationToken: cancellationToken);

                // Fetch all policies for the project/session to calculate the global next version
                var allProjectPolicies = request.ProjectId.HasValue
                    ? await _policyRepository.FindAsync(p => p.ProjectId == request.ProjectId.Value && p.Scope == PolicyScope.Project)
                    : await _policyRepository.FindAsync(p => p.RequirementSessionId == request.RequirementSessionId!.Value && p.Scope == PolicyScope.Project);

                int nextVersion = allProjectPolicies.Any() ? allProjectPolicies.Max(p => p.VersionNumber) + 1 : 1;

                // Only deactivate previous versions of the specific document being uploaded
                var existingPolicies = allProjectPolicies.Where(p => p.TitleEn == fileName).ToList();

                if (existingPolicies.Any())
                {
                    foreach (var oldPolicy in existingPolicies)
                    {
                        oldPolicy.IsActive = false;
                        _policyRepository.Update(oldPolicy);
                    }
                }

                foreach (var chunk in chunks)
                {
                    if (request.ProjectId.HasValue) chunk.ProjectId = request.ProjectId.Value;
                    if (request.RequirementSessionId.HasValue) chunk.RequirementSessionId = request.RequirementSessionId.Value;
                    
                    chunk.Category = category;
                    chunk.SourceFile = fileName;
                    chunk.DocumentType = "ProjectPolicy";
                    
                    // Option A enforcement
                    chunk.VersionNumber = nextVersion;
                    chunk.IsActive = true;
                }

                await _vectorStore.UpsertAsync(KnowledgeCollectionType.ProjectPolicies, chunks, cancellationToken);

                var policy = new Policy
                {
                    Scope = PolicyScope.Project,
                    ProjectId = request.ProjectId,
                    RequirementSessionId = request.RequirementSessionId,
                    TitleEn = fileName,
                    TitleAr = fileName,
                    ContentEn = extractedText,
                    DocumentUrl = documentUrl,
                    CloudinaryPublicId = cloudinaryPublicId,
                    DocumentId = documentId,
                    AiStatus = AiProcessingStatus.Completed,
                    VersionNumber = nextVersion,
                    IsActive = true
                };

                await _policyRepository.AddAsync(policy);

                await saveChangesAsync(cancellationToken);

                return Result.Success(new UploadProjectPolicyResponse
                {
                    DocumentId = documentId,
                    Message = "Document ingested successfully.",
                    ChunksCreated = chunks.Count
                });
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task<Result<ProjectPolicyAnswerResponse>> AskAsync(
            ProjectPolicyQuestionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return Result.Failure<ProjectPolicyAnswerResponse>(CommonErrors.InvalidInput("Question cannot be null or empty."));
                
            if (request.Question.Trim().Length < 3)
                return Result.Failure<ProjectPolicyAnswerResponse>(CommonErrors.InvalidInput("Question must be at least 3 characters long."));
                
            if (request.Question.Length > 500)
                return Result.Failure<ProjectPolicyAnswerResponse>(CommonErrors.InvalidInput("Question cannot exceed 500 characters."));

            if ((request.ProjectId.HasValue && request.RequirementSessionId.HasValue) ||
                (!request.ProjectId.HasValue && !request.RequirementSessionId.HasValue))
            {
                return Result.Failure<ProjectPolicyAnswerResponse>(
                    request.ProjectId.HasValue ? KnowledgeErrors.AmbiguousTenantIdentifier : KnowledgeErrors.MissingProjectPolicyIdentifier);
            }
            
            if (request.ProjectId.HasValue && request.ProjectId.Value == Guid.Empty)
                return Result.Failure<ProjectPolicyAnswerResponse>(CommonErrors.InvalidInput("ProjectId cannot be empty Guid."));
                
            if (request.RequirementSessionId.HasValue && request.RequirementSessionId.Value == Guid.Empty)
                return Result.Failure<ProjectPolicyAnswerResponse>(CommonErrors.InvalidInput("RequirementSessionId cannot be empty Guid."));

            if (request.ProjectId.HasValue)
            {
                var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value);
                if (project == null)
                {
                    return Result.Failure<ProjectPolicyAnswerResponse>(ProjectErrors.NotFound);
                }
            }

            bool hasDocuments = request.ProjectId.HasValue 
                ? await _policyRepository.AnyAsync(p => p.ProjectId == request.ProjectId.Value && p.Scope == PolicyScope.Project)
                : await _policyRepository.AnyAsync(p => p.RequirementSessionId == request.RequirementSessionId!.Value && p.Scope == PolicyScope.Project);

            if (!hasDocuments)
            {
                var msg = _localizationService.GetString("NO_POLICIES_UPLOADED");
                return Result.Success(new ProjectPolicyAnswerResponse
                {
                    Answer = msg,
                    Sources = new List<ProjectPolicySourceDto>()
                });
            }

            var collectionType = KnowledgeCollectionType.ProjectPolicies;

            var result = await _knowledgeOrchestrator.AskAsync(
                collectionType,
                requirementSessionId: request.RequirementSessionId,
                projectId: request.ProjectId,
                companyId: null,
                question: request.Question,
                cancellationToken: cancellationToken);

            if (result.IsFailure)
            {
                return Result.Failure<ProjectPolicyAnswerResponse>(result.Error);
            }

            var response = new ProjectPolicyAnswerResponse
            {
                Answer = result.Value.Answer,
                Sources = result.Value.Sources.Select(s => new ProjectPolicySourceDto
                {
                    FileName = s.FileName,
                    Category = collectionType.ToDisplayName()
                }).ToList()
            };

            return Result.Success(response);
        }

        public async Task<Result> PromoteAsync(
            PromoteProjectPolicyRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.RequirementSessionId == Guid.Empty || request.ProjectId == Guid.Empty)
            {
                return Result.Failure(KnowledgeErrors.MissingProjectPolicyIdentifier);
            }

            var project = await _projectRepository.GetByIdAsync(request.ProjectId);
            if (project == null)
            {
                return Result.Failure(ProjectErrors.NotFound);
            }

            var policiesToPromote = await _policyRepository.FindAsync(p => p.RequirementSessionId == request.RequirementSessionId && p.Scope == PolicyScope.Project);
            
            if (!policiesToPromote.Any())
            {
                return Result.Success();
            }

            var policiesToUpdate = new List<Policy>();

            foreach (var policy in policiesToPromote)
            {
                if (policy.ProjectId == request.ProjectId) continue;

                if (policy.DocumentId.HasValue)
                {
                    await _vectorStore.PromoteKnowledgeAsync(
                        KnowledgeCollectionType.ProjectPolicies,
                        request.ProjectId,
                        policy.DocumentId.Value,
                        cancellationToken);
                }

                policy.ProjectId = request.ProjectId;
                policy.RequirementSessionId = null;
                policiesToUpdate.Add(policy);
            }

            if (policiesToUpdate.Any())
            {
                _policyRepository.UpdateRange(policiesToUpdate);
            }

            return Result.Success();
        }

        public async Task<Result<List<ProjectPolicyDocumentDto>>> GetPoliciesAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            if (projectId == Guid.Empty)
            {
                return Result.Failure<List<ProjectPolicyDocumentDto>>(CommonErrors.InvalidInput("ProjectId cannot be empty Guid."));
            }

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<List<ProjectPolicyDocumentDto>>(ProjectErrors.NotFound);
            }

            var policies = await _policyRepository.FindAsync(p => p.ProjectId == projectId && p.Scope == PolicyScope.Project);

            var dtos = policies.Select(p => new ProjectPolicyDocumentDto
            {
                PolicyId = p.Id,
                Title = p.TitleEn,
                UploadDate = p.CreatedAt,
                Category = p.TitleEn, // Fallback since category isn't in SQL, can be derived or left as Title
                Version = p.VersionNumber,
                FileName = p.TitleEn,
                AiStatus = p.AiStatus.ToString()
            }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result> DeleteAsync(
            Guid documentId,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure(ProjectErrors.NotFound);
            }

            var policy = await _policyRepository.FindSingleAsync(p => p.DocumentId == documentId);
            if (policy == null || policy.ProjectId != projectId || policy.Scope != PolicyScope.Project)
            {
                return Result.Failure(CommonErrors.NotFound("Project Policy Document"));
            }

            if (!string.IsNullOrEmpty(policy.CloudinaryPublicId))
            {
                var cloudResult = await _fileStorage.DeleteFileAsync(policy.CloudinaryPublicId);
                if (!cloudResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to delete Cloudinary file {PublicId} for Policy {PolicyId}. Error: {Error}", policy.CloudinaryPublicId, policy.Id, cloudResult.Error?.Code);
                }
            }

            try
            {
                await _vectorStore.DeleteAsync(
                    KnowledgeCollectionType.ProjectPolicies,
                    documentId,
                    requirementSessionId: null,
                    projectId: projectId,
                    companyId: null,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete vectors for Document {DocumentId}. SQL record will NOT be deleted.", documentId);
                return Result.Failure(CommonErrors.ServerError("Failed to delete vectors."));
            }

            _policyRepository.Delete(policy);

            return Result.Success();
        }
    }
}
