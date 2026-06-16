using Microsoft.AspNetCore.Http;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Models.Session;
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
            CancellationToken cancellationToken)
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

            try
            {
                // 1. Find matched extractor
                var extractor = _extractors.FirstOrDefault(e => e.CanHandle(file.ContentType, file.FileName));
                if (extractor == null)
                {
                    return new DocumentIngestionResult
                    {
                        Success = false,
                        Message = $"Unsupported file type: {file.ContentType} ({file.FileName})"
                    };
                }

                // 2. Extract Text
                string extractedText;
                using (var stream = file.OpenReadStream())
                {
                    extractedText = await extractor.ExtractTextAsync(stream, cancellationToken);
                }

                // 3. Categorize Document
                var category = await _categorizationAgent.CategorizeAsync(file.FileName, extractedText, cancellationToken);

                // 4. Create Ingested Document
                var documentId = Guid.NewGuid();
                var document = new IngestedDocument
                {
                    Id = documentId,
                    FileName = file.FileName,
                    Category = category,
                    ContentType = file.ContentType,
                    FileSize = file.Length,
                    ExtractedText = extractedText,
                    UploadedAt = DateTime.UtcNow,
                    CloudinaryUrl = string.Empty
                };

                // 5. Chunk Content
                var chunks = await _chunkingAgent.ChunkContentAsync(documentId, extractedText, cancellationToken: cancellationToken);

                // 6. Save document and chunks to document store
                await _documentStore.SaveDocumentAsync(document, cancellationToken);
                await _documentStore.SaveChunksAsync(chunks, cancellationToken);

                // 7. Update Session Knowledge Context
                session.Knowledge.Documents.Add(document);
                session.Knowledge.DocumentIds.Add(document.Id);

                // 8. Record audit entry
                session.AddDecision(
                    nameof(DocumentIngestionOrchestrator),
                    "Document ingested successfully");

                // Save session changes
                await _sessionStore.SaveAsync(session, cancellationToken);

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
