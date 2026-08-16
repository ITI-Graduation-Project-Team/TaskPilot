using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.CompanyPolicies;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Extensions;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;

namespace TaskPilot.Services
{
    public class CompanyPolicyService : ICompanyPolicyService
    {
        private readonly IEnumerable<IDocumentTextExtractor> _extractors;
        private readonly DocumentCategorizationAgent _categorizationAgent;
        private readonly ChunkingAgent _chunkingAgent;
        private readonly IVectorStore _vectorStore;
        private readonly KnowledgeOrchestrator _knowledgeOrchestrator;
        private readonly IRepository<Policy> _policyRepository;
        private readonly IRepository<Company> _companyRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<CompanyPolicyService> _logger;
        private readonly IEntitlementService _entitlementService;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> _companyLocks = new();

        public CompanyPolicyService(
            IEnumerable<IDocumentTextExtractor> extractors,
            DocumentCategorizationAgent categorizationAgent,
            ChunkingAgent chunkingAgent,
            IVectorStore vectorStore,
            KnowledgeOrchestrator knowledgeOrchestrator,
            IRepository<Policy> policyRepository,
            IRepository<Company> companyRepository,
            IUnitOfWork unitOfWork,
            IFileStorageService fileStorage,
            ILogger<CompanyPolicyService> logger,
            IEntitlementService entitlementService)
        {
            _extractors = extractors;
            _categorizationAgent = categorizationAgent;
            _chunkingAgent = chunkingAgent;
            _vectorStore = vectorStore;
            _knowledgeOrchestrator = knowledgeOrchestrator;
            _policyRepository = policyRepository;
            _companyRepository = companyRepository;
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _logger = logger;
            _entitlementService = entitlementService;
        }

        public async Task<Result<UploadCompanyPolicyResponse>> IngestAsync(IngestCompanyPolicyRequest request, Func<CancellationToken, Task> saveChangesAsync, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(request.CompanyId);
            if (company == null)
            {
                return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.NotFound("Company"));
            }

            bool hasFile = request.File != null && request.File.Length > 0;
            bool hasContent = !string.IsNullOrWhiteSpace(request.ContentEn);

            if (!hasFile && !hasContent)
            {
                return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.InvalidInput("Both file and content cannot be empty."));
            }

            string extractedText = string.Empty;
            string fileName = request.TitleEn ?? "Policy";

            if (hasFile)
            {
                fileName = request.TitleEn ?? request.File!.FileName;
                var extractor = _extractors.FirstOrDefault(e => e.CanHandle(request.File!.ContentType, request.File.FileName));
                if (extractor == null)
                {
                    return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.InvalidInput($"Unsupported file type: {request.File!.ContentType} ({request.File.FileName})"));
                }

                using (var stream = request.File!.OpenReadStream())
                {
                    extractedText = await extractor.ExtractTextAsync(stream, cancellationToken);
                }
            }
            else
            {
                extractedText = request.ContentEn!;
            }

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.InvalidInput("Extracted text is empty."));
            }

            // Generate deterministic ID
            using var md5Doc = System.Security.Cryptography.MD5.Create();
            var hashInput = $"{request.CompanyId}_{extractedText}";
            var documentId = new Guid(md5Doc.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashInput)));

            var semaphore = _companyLocks.GetOrAdd(request.CompanyId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // Source of truth for duplicates: DocumentId
                var policyExists = await _policyRepository.FindSingleAsync(p => p.DocumentId == documentId || p.DocumentPublicId == documentId.ToString());
                if (policyExists != null)
                {
                    _logger.LogInformation("Company Policy document {FileName} already exists for Company {CompanyId} with DocumentId {DocumentId}. Skipping ingestion.", fileName, request.CompanyId, documentId);
                    return Result.Success(new UploadCompanyPolicyResponse
                    {
                        DocumentId = documentId,
                        Message = "Document already ingested.",
                        ChunksCreated = 0
                    });
                }

                // Upload to Cloudinary if needed
                string? documentUrl = request.DocumentUrl;
                string? cloudinaryPublicId = request.CloudinaryPublicId;

                if (hasFile && string.IsNullOrEmpty(documentUrl) && !request.SkipCloudUpload)
                {
                    var entitlementResult = await _entitlementService.EnsureCanUploadAsync(company.OwnerId, request.File!.Length, 0, cancellationToken);
                    if (entitlementResult.IsFailure) return Result.Failure<UploadCompanyPolicyResponse>(entitlementResult.Error);

                    var uploadResult = await _fileStorage.UploadFileAsync(request.File!, $"taskpilot/companies/{request.CompanyId}/policies");
                    if (!uploadResult.IsSuccess)
                    {
                        return Result.Failure<UploadCompanyPolicyResponse>(uploadResult.Error!);
                    }
                    documentUrl = uploadResult.Value.Url;
                    cloudinaryPublicId = uploadResult.Value.PublicId;

                    await _entitlementService.UpdateStorageUsageAsync(company.OwnerId, request.File!.Length, cancellationToken);
                }

                var category = await _categorizationAgent.CategorizeAsync(fileName, extractedText, Guid.Empty, cancellationToken);

                var chunks = await _chunkingAgent.ChunkContentAsync(documentId, extractedText, cancellationToken: cancellationToken);

                foreach (var chunk in chunks)
                {
                    chunk.CompanyId = request.CompanyId;
                    chunk.Category = category;
                    chunk.SourceFile = fileName;
                    chunk.DocumentType = "CompanyPolicy";
                }

                await _vectorStore.UpsertAsync(KnowledgeCollectionType.CompanyPolicies, chunks, cancellationToken);

                var policies = await _policyRepository.FindAsync(p => p.CompanyId == request.CompanyId);
                int nextVersion = (policies.Any() ? policies.Max(p => p.VersionNumber) : 0) + 1;

                var policy = new Policy
                {
                    Scope = PolicyScope.Company,
                    CompanyId = request.CompanyId,
                    TitleEn = fileName,
                    TitleAr = request.TitleAr ?? fileName,
                    ContentEn = request.ContentEn ?? extractedText,
                    ContentAr = request.ContentAr,
                    DocumentUrl = documentUrl,
                    CloudinaryPublicId = cloudinaryPublicId,
                    DocumentId = documentId,
                    DocumentPublicId = documentId.ToString(), // Maintain legacy compat
                    AiStatus = AiProcessingStatus.Completed,
                    VersionNumber = nextVersion,
                    FileSize = (hasFile && !string.IsNullOrEmpty(documentUrl) && !request.SkipCloudUpload) ? request.File!.Length : 0
                };

                await _policyRepository.AddAsync(policy);

                await saveChangesAsync(cancellationToken);

                return Result.Success(new UploadCompanyPolicyResponse
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

        public async Task<Result<CompanyPolicyAnswerResponse>> AskAsync(CompanyPolicyQuestionRequest request, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(request.CompanyId);
            if (company == null)
            {
                return Result.Failure<CompanyPolicyAnswerResponse>(CommonErrors.NotFound("Company"));
            }

            var collectionType = KnowledgeCollectionType.CompanyPolicies;

            var result = await _knowledgeOrchestrator.AskAsync(
                collectionType,
                requirementSessionId: null,
                projectId: null,
                companyId: request.CompanyId,
                question: request.Question,
                cancellationToken: cancellationToken);

            if (result.IsFailure)
            {
                return Result.Failure<CompanyPolicyAnswerResponse>(result.Error);
            }

            var response = new CompanyPolicyAnswerResponse
            {
                Answer = result.Value.Answer,
                Sources = result.Value.Sources.Select(s => new CompanyPolicySourceDto
                {
                    FileName = s.FileName,
                    Category = collectionType.ToDisplayName()
                }).ToList()
            };

            return Result.Success(response);
        }

        public async Task<Result<List<CompanyPolicyDocumentDto>>> GetDocumentsAsync(Guid companyId, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(companyId);
            if (company == null)
            {
                return Result.Failure<List<CompanyPolicyDocumentDto>>(CommonErrors.NotFound("Company"));
            }

            var policies = await _policyRepository.FindAsync(p => p.CompanyId == companyId && p.Scope == PolicyScope.Company);

            var dtos = policies.Select(p => {
                Guid docId = p.Id;
                if (!string.IsNullOrEmpty(p.DocumentPublicId) && Guid.TryParse(p.DocumentPublicId, out var parsed))
                {
                    docId = parsed;
                }
                else if (p.DocumentId.HasValue && p.DocumentId.Value != Guid.Empty)
                {
                    docId = p.DocumentId.Value;
                }
                
                return new CompanyPolicyDocumentDto
                {
                    DocumentId = docId,
                    FileName = p.TitleEn,
                    UploadedAt = p.CreatedAt
                };
            }).ToList();

            return Result.Success(dtos);
        }

        public async Task<Result> DeleteDocumentAsync(Guid companyId, Guid documentId, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(companyId);
            if (company == null)
            {
                return Result.Failure(CommonErrors.NotFound("Company"));
            }

            var policy = await _policyRepository.FindSingleAsync(p => p.DocumentPublicId == documentId.ToString());
            if (policy == null || policy.CompanyId != companyId || policy.Scope != PolicyScope.Company)
            {
                return Result.Failure(CommonErrors.NotFound("Company Policy Document"));
            }

            await _vectorStore.DeleteAsync(
                KnowledgeCollectionType.CompanyPolicies,
                documentId,
                requirementSessionId: null,
                projectId: null,
                companyId: companyId,
                cancellationToken: cancellationToken);

            _policyRepository.Delete(policy);

            await _entitlementService.UpdateStorageUsageAsync(company.OwnerId, -policy.FileSize, cancellationToken);

            return Result.Success();
        }
    }
}
