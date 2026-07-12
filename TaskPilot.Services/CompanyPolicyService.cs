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
using TaskPilot.Services.Interfaces;

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
        private readonly ILogger<CompanyPolicyService> _logger;

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
            ILogger<CompanyPolicyService> logger)
        {
            _extractors = extractors;
            _categorizationAgent = categorizationAgent;
            _chunkingAgent = chunkingAgent;
            _vectorStore = vectorStore;
            _knowledgeOrchestrator = knowledgeOrchestrator;
            _policyRepository = policyRepository;
            _companyRepository = companyRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<UploadCompanyPolicyResponse>> UploadAsync(UploadCompanyPolicyRequest request, Func<CancellationToken, Task> saveChangesAsync, CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(request.CompanyId);
            if (company == null)
            {
                return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.NotFound("Company"));
            }

            if (request.File == null || request.File.Length == 0)
            {
                return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.InvalidInput("File is empty or missing."));
            }

            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(request.File.ContentType, request.File.FileName));
            if (extractor == null)
            {
                return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.InvalidInput($"Unsupported file type: {request.File.ContentType} ({request.File.FileName})"));
            }

            string extractedText;
            using (var stream = request.File.OpenReadStream())
            {
                extractedText = await extractor.ExtractTextAsync(stream, cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Result.Failure<UploadCompanyPolicyResponse>(CommonErrors.InvalidInput("Extracted text is empty."));
            }

            using var md5Doc = System.Security.Cryptography.MD5.Create();
            var hashInput = $"{request.CompanyId}_{extractedText}";
            var documentId = new Guid(md5Doc.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashInput)));

            var semaphore = _companyLocks.GetOrAdd(request.CompanyId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                // Source of truth for duplicates: DocumentPublicId
                var policyExists = await _policyRepository.FindSingleAsync(p => p.DocumentPublicId == documentId.ToString());
                if (policyExists != null)
                {
                    _logger.LogInformation("Company Policy document {FileName} already exists for Company {CompanyId} with DocumentId {DocumentId}. Skipping ingestion.", request.File.FileName, request.CompanyId, documentId);
                    return Result.Success(new UploadCompanyPolicyResponse
                    {
                        DocumentId = documentId,
                        Message = "Document already ingested.",
                        ChunksCreated = 0
                    });
                }

                var category = await _categorizationAgent.CategorizeAsync(request.File.FileName, extractedText, cancellationToken);

                var chunks = await _chunkingAgent.ChunkContentAsync(documentId, extractedText, cancellationToken: cancellationToken);

                foreach (var chunk in chunks)
                {
                    chunk.CompanyId = request.CompanyId;
                    chunk.Category = category;
                    chunk.SourceFile = request.File.FileName;
                    chunk.DocumentType = "CompanyPolicy";
                }

                await _vectorStore.UpsertAsync(KnowledgeCollectionType.CompanyPolicies, chunks, cancellationToken);

                var policies = await _policyRepository.FindAsync(p => p.CompanyId == request.CompanyId);
                int nextVersion = (policies.Any() ? policies.Max(p => p.VersionNumber) : 0) + 1;

                var policy = new Policy
                {
                    Scope = PolicyScope.Company,
                    CompanyId = request.CompanyId,
                    TitleEn = request.File.FileName,
                    TitleAr = request.File.FileName,
                    ContentEn = extractedText,
                    DocumentPublicId = documentId.ToString(),
                    AiStatus = AiProcessingStatus.Completed,
                    VersionNumber = nextVersion
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

            var result = await _knowledgeOrchestrator.AskAsync(
                KnowledgeCollectionType.CompanyPolicies,
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
                    Category = s.Category.ToString()
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

            var dtos = policies.Select(p => new CompanyPolicyDocumentDto
            {
                DocumentId = !string.IsNullOrEmpty(p.DocumentPublicId) ? Guid.Parse(p.DocumentPublicId) : p.Id,
                FileName = p.TitleEn,
                UploadedAt = p.CreatedAt
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

            return Result.Success();
        }
    }
}
