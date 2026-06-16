using Microsoft.AspNetCore.Http;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Orchestrators
{
    public class DocumentIngestionOrchestrator
    {
        private readonly IEnumerable<IDocumentTextExtractor> _extractors;
        private readonly DocumentCategorizationAgent _categorizationAgent;
        private readonly ChunkingAgent _chunkingAgent;
        private readonly IDocumentStore _documentStore;
        private readonly IRequirementSessionStore _sessionStore;

        public DocumentIngestionOrchestrator(
            IEnumerable<IDocumentTextExtractor> extractors,
            DocumentCategorizationAgent categorizationAgent,
            ChunkingAgent chunkingAgent,
            IDocumentStore documentStore,
            IRequirementSessionStore sessionStore)
        {
            _extractors = extractors;
            _categorizationAgent = categorizationAgent;
            _chunkingAgent = chunkingAgent;
            _documentStore = documentStore;
            _sessionStore = sessionStore;
        }

        public async Task<DocumentIngestionResult> IngestAsync(
            Guid sessionId,
            IFormFile file,
            Guid? projectId = null,
            bool isAvailableToContextSummarizer = true,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionStore.GetAsync(sessionId, cancellationToken);
            if (session == null)
            {
                return new DocumentIngestionResult
                {
                    Success = false,
                    Message = "Session not found."
                };
            }

            var result =
                await IngestCoreAsync(
                    file,
                    projectId,
                    isAvailableToContextSummarizer,
                    cancellationToken);

            if (!result.Success || result.DocumentId == Guid.Empty)
            {
                return result;
            }

            var document =
                await _documentStore
                    .GetDocumentAsync(result.DocumentId, cancellationToken);

            if (document is not null)
            {
                session.Knowledge.Documents.Add(document);
                session.Knowledge.DocumentIds.Add(document.Id);
            }

            session.AddDecision(
                nameof(DocumentIngestionOrchestrator),
                "Document ingested successfully");

            await _sessionStore.SaveAsync(session, cancellationToken);

            return result;
        }

        public Task<DocumentIngestionResult> IngestProjectKnowledgeAsync(
            IFormFile file,
            Guid? projectId = null,
            bool isAvailableToContextSummarizer = true,
            CancellationToken cancellationToken = default)
        {
            return IngestCoreAsync(
                file,
                projectId,
                isAvailableToContextSummarizer,
                cancellationToken);
        }

        private async Task<DocumentIngestionResult> IngestCoreAsync(
            IFormFile file,
            Guid? projectId,
            bool isAvailableToContextSummarizer,
            CancellationToken cancellationToken)
        {
            try
            {
                var extractor =
                    _extractors
                        .FirstOrDefault(e => e.CanHandle(file.ContentType, file.FileName));

                if (extractor == null)
                {
                    return new DocumentIngestionResult
                    {
                        Success = false,
                        Message = $"Unsupported file type: {file.ContentType} ({file.FileName})"
                    };
                }

                string extractedText;

                using (var stream = file.OpenReadStream())
                {
                    extractedText =
                        await extractor
                            .ExtractTextAsync(stream, cancellationToken);
                }

                var category =
                    await _categorizationAgent
                        .CategorizeAsync(file.FileName, extractedText, cancellationToken);

                var documentId = Guid.NewGuid();
                var document =
                    new IngestedDocument
                    {
                        Id = documentId,
                        ProjectId = projectId,
                        FileName = file.FileName,
                        Category = category,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        ExtractedText = extractedText,
                        IsAvailableToContextSummarizer = isAvailableToContextSummarizer,
                        UploadedAt = DateTime.UtcNow,
                        CloudinaryUrl = string.Empty
                    };

                var chunks =
                    await _chunkingAgent
                        .ChunkContentAsync(
                            documentId,
                            extractedText,
                            projectId,
                            cancellationToken: cancellationToken);

                await _documentStore
                    .SaveDocumentAsync(document, cancellationToken);

                await _documentStore
                    .SaveChunksAsync(chunks, cancellationToken);

                return new DocumentIngestionResult
                {
                    Success = true,
                    DocumentId = document.Id,
                    Category = category,
                    ChunksCreated = chunks.Count,
                    QuestionsAutoResolved = 0,
                    Message = "Document ingested successfully."
                };
            }
            catch (Exception ex)
            {
                return new DocumentIngestionResult
                {
                    Success = false,
                    Message = $"Error during document ingestion: {ex.Message}"
                };
            }
        }
    }
}
