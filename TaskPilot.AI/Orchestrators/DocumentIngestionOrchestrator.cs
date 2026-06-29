using Microsoft.AspNetCore.Http;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Agents.Requirements;
using Microsoft.Extensions.Logging;

namespace TaskPilot.AI.Orchestrators
{
    public class DocumentIngestionOrchestrator
    {
        private readonly IEnumerable<IDocumentTextExtractor> _extractors;
        private readonly DocumentCategorizationAgent _categorizationAgent;
        private readonly ChunkingAgent _chunkingAgent;
        private readonly IDocumentStore _documentStore;
        private readonly IRequirementSessionStore _sessionStore;
        private readonly DocumentQuestionResolutionAgent _documentQuestionResolutionAgent;
        private readonly CompletenessEvaluatorAgent _completenessEvaluatorAgent;
        private readonly IVectorStore _vectorStore;
        private readonly ILogger<DocumentIngestionOrchestrator> _logger;
        private readonly RequirementsBuilderAgent _builderAgent;

        public DocumentIngestionOrchestrator(
            IEnumerable<IDocumentTextExtractor> extractors,
            DocumentCategorizationAgent categorizationAgent,
            ChunkingAgent chunkingAgent,
            IDocumentStore documentStore,
            IRequirementSessionStore sessionStore,
            DocumentQuestionResolutionAgent documentQuestionResolutionAgent,
            CompletenessEvaluatorAgent completenessEvaluatorAgent,
            IVectorStore vectorStore,
            ILogger<DocumentIngestionOrchestrator> logger,
            RequirementsBuilderAgent builderAgent)
        {
            _extractors = extractors;
            _categorizationAgent = categorizationAgent;
            _chunkingAgent = chunkingAgent;
            _documentStore = documentStore;
            _sessionStore = sessionStore;
            _documentQuestionResolutionAgent = documentQuestionResolutionAgent;
            _completenessEvaluatorAgent = completenessEvaluatorAgent;
            _vectorStore = vectorStore;
            _logger = logger;
            _builderAgent = builderAgent;
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

                _logger.LogInformation("Extracted Text Preview: {Preview}", extractedText.Length > 200 ? extractedText.Substring(0, 200) : extractedText);

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

                _logger.LogInformation(
                    "Chunk generation completed. Total chunks: {Count}",
                    chunks.Count);

                foreach (var chunk in chunks)
                {
                    _logger.LogInformation(
                        "Chunk {Index}: {Preview}",
                        chunk.ChunkIndex,
                        chunk.Content[..Math.Min(chunk.Content.Length, 150)]);
                }

                // 6. Save document and chunks to document store
                await _documentStore.SaveDocumentAsync(document, cancellationToken);
                await _documentStore.SaveChunksAsync(chunks, cancellationToken);

                // 6.5 Vector Store Upsert
                foreach (var chunk in chunks)
                {
                    chunk.SessionId = sessionId;
                    chunk.Category = category;
                }
                
                _logger.LogInformation("Preparing to store {Count} chunks in vector store", chunks.Count);
                await _vectorStore.UpsertAsync(chunks, cancellationToken);
                _logger.LogInformation("Successfully stored {Count} chunks in vector store", chunks.Count);

                session.AddDecision(
                    "QdrantVectorStore",
                    $"Stored {chunks.Count} chunks in vector store for document {documentId}");

                // 7. Update Session Knowledge Context
                session.Knowledge.Documents.Add(document);
                session.Knowledge.DocumentIds.Add(document.Id);

                // 8. Record audit entry
                session.AddDecision(
                    nameof(DocumentIngestionOrchestrator),
                    "Document ingested successfully");

                session.UpdatedAt = DateTime.UtcNow;

                // Save session changes
                await _sessionStore.SaveAsync(session, cancellationToken);

                // 9. Document Question Resolution
                int questionsAutoResolved = 0;
                var unansweredQuestions = session.UnansweredQuestions;

                if (unansweredQuestions.Any())
                {
                    var resolutions = await _documentQuestionResolutionAgent.ResolveAsync(unansweredQuestions, extractedText);

                    foreach (var resolution in resolutions.Where(r => r.IsAnswered))
                    {
                        var question = session.QuestionPool.FirstOrDefault(q => q.Id == resolution.QuestionId);
                        if (question != null && !question.IsAnswered)
                        {
                            question.IsAnswered = true;
                            question.Answer = resolution.ExtractedAnswer;
                            question.AnsweredAt = DateTime.UtcNow;
                            question.AnsweredFromSource = "Document";
                            questionsAutoResolved++;
                        }
                    }

                    if (questionsAutoResolved > 0)
                    {
                        session.AddDecision(
                            nameof(DocumentIngestionOrchestrator),
                            $"Auto-resolved {questionsAutoResolved} questions from document.");

                        // 10. Re-run Completeness Evaluator
                        session.CompletenessReport = await _completenessEvaluatorAgent.EvaluateAsync(session);

                        session.AddDecision(
                            nameof(CompletenessEvaluatorAgent),
                            $"Re-evaluated completeness after document ingestion. New score: {session.CompletenessReport.Score}");

                        // NEW: if all questions are now answered, complete the workflow
                        if (session.AllQuestionsAnswered && 
                            session.CompletenessReport?.ReadyForPlanning == true && 
                            session.FinalRequirements is null)
                        {
                            session.FinalRequirements = await _builderAgent.BuildAsync(session, cancellationToken);

                            session.Status = RequirementSessionStatus.Planning;

                            session.AddDecision(
                                nameof(RequirementsBuilderAgent),
                                "Final requirements built after document resolved all pending questions.");
                        }

                        session.UpdatedAt = DateTime.UtcNow;

                        // Save session changes again
                        await _sessionStore.SaveAsync(session, cancellationToken);
                    }
                }

                return new DocumentIngestionResult
                {
                    Success = true,
                    DocumentId = document.Id,
                    Category = category,
                    ChunksCreated = chunks.Count,
                    QuestionsAutoResolved = questionsAutoResolved,
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
