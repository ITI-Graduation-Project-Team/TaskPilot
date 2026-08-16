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
using TaskPilot.Models.Enums;

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
        private readonly RequirementAnalysisAgent _requirementAnalysisAgent;

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
            RequirementsBuilderAgent builderAgent,
            RequirementAnalysisAgent requirementAnalysisAgent)
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
            _requirementAnalysisAgent = requirementAnalysisAgent;
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
                var existingDoc = session.Knowledge.Documents.FirstOrDefault(d => d.FileName == file.FileName && d.FileSize == file.Length);
                if (existingDoc != null)
                {
                    _logger.LogInformation("Document {FileName} already exists in session {SessionId}. Skipping ingestion.", file.FileName, sessionId);
                    return new DocumentIngestionResult
                    {
                        Success = true,
                        DocumentId = existingDoc.Id,
                        Category = existingDoc.Category,
                        ChunksCreated = 0,
                        QuestionsAutoResolved = 0,
                        Message = "Document already ingested."
                    };
                }

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
                var category = await _categorizationAgent.CategorizeAsync(file.FileName, extractedText, session.ProjectId.GetValueOrDefault(), cancellationToken);

                // 4. Create Ingested Document
                using var md5Doc = System.Security.Cryptography.MD5.Create();
                var hashInput = $"{sessionId}_{extractedText}";
                var documentId = new Guid(md5Doc.ComputeHash(System.Text.Encoding.UTF8.GetBytes(hashInput)));
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
                    chunk.RequirementSessionId = sessionId;
                    chunk.Category = category;
                    chunk.SourceFile = file.FileName;
                    chunk.DocumentType = file.ContentType;
                }
                
                _logger.LogInformation("Preparing to store {Count} chunks in vector store", chunks.Count);
                await _vectorStore.UpsertAsync(KnowledgeCollectionType.ProjectPolicies, chunks, cancellationToken);
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
                    var resolutions = await _documentQuestionResolutionAgent.ResolveAsync(unansweredQuestions, extractedText, session.ProjectId.GetValueOrDefault());

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

                // Auto-resolve the BRD prompt question if it exists (chat-first path)
                var brdPrompt = session.QuestionPool
                    .FirstOrDefault(q => q.IsBrdPrompt && !q.IsAnswered);

                if (brdPrompt is not null)
                {
                    brdPrompt.IsAnswered         = true;
                    brdPrompt.Answer             = $"Document uploaded: {file.FileName}";
                    brdPrompt.AnsweredFromSource = "Document";
                    brdPrompt.AnsweredAt         = DateTime.UtcNow;
                    session.IsLimitedMode        = false;

                    session.AddDecision("Workflow", "BRD prompt resolved â€” document received.");

                    // Replace generic questions with BRD-specific gap questions
                    await ReplaceClarificationQuestionsFromBrdAsync(session, cancellationToken);

                    session.UpdatedAt = DateTime.UtcNow;
                    await _sessionStore.SaveAsync(session, cancellationToken);
                }
                else
                {
                    // Document-first path: session was created via StartWithDocumentAsync().
                    // No BRD sentinel question exists in the pool, so the guard above was always
                    // false and RequirementAnalysisAgent was never invoked.
                    // Run analysis directly â€” this is the primary BRD-first entry point.
                    _logger.LogInformation(
                        "Document-first path detected for session {SessionId}. " +
                        "Running RequirementAnalysisAgent directly.",
                        sessionId);

                    await ReplaceClarificationQuestionsFromBrdAsync(session, cancellationToken);

                    session.UpdatedAt = DateTime.UtcNow;
                    await _sessionStore.SaveAsync(session, cancellationToken);
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

        private async Task ReplaceClarificationQuestionsFromBrdAsync(
            RequirementSession session,
            CancellationToken cancellationToken)
        {
            var analysis = await _requirementAnalysisAgent
                .AnalyzeAsync(session, cancellationToken);

            // Remove all unanswered non-BRD-prompt questions (generated without BRD context)
            session.QuestionPool.RemoveAll(q => !q.IsAnswered && !q.IsBrdPrompt);

            // Merge requirements extracted from BRD
            session.Requirements.MergeFrom(analysis.ExtractedRequirements);

            // Add BRD-specific gap questions with full metadata (0â€“6 max)
            foreach (var gapQuestion in analysis.GapQuestions)
            {
                session.QuestionPool.Add(new TaskPilot.AI.Models.Questions.ClarificationQuestion
                {
                    Id          = Guid.NewGuid(),
                    Question    = gapQuestion.Question,
                    Category    = MapCategory(gapQuestion.Category),
                    Priority    = MapPriority(gapQuestion.Priority),
                    Reason                       = gapQuestion.Reason,
                    MissingItems                 = gapQuestion.MissingItems,
                    BusinessImpact               = gapQuestion.BusinessImpact,
                    EstimatedEffectOnCompleteness = gapQuestion.EstimatedEffectOnCompleteness,
                    IsBrdPrompt = false
                });
            }

            // Store enriched confidence scores in session
            session.ConfidenceScores = analysis.ConfidenceScores
                .Select(c => new RequirementConfidenceScore
                {
                    Category       = c.Category,
                    Score          = c.Score,
                    Status         = c.Status,
                    ExtractedValue = c.ExtractedValue,
                    Reason         = c.Reason,
                    Evidence       = c.Evidence,
                    MissingItems   = c.MissingItems
                }).ToList();

            // Synthesize CompletenessReport, preferring the AIâ€™s weighted overall score
            // over a naive average, and using the AIâ€™s explicit FinalizeReadiness flag.
            session.RequirementCompletenessReport = analysis.RequirementCompletenessReport;

            var aiScore = analysis.RequirementCompletenessReport?.OverallCompleteness > 0
                ? analysis.RequirementCompletenessReport.OverallCompleteness / 100f
                : analysis.ConfidenceScores.Count > 0
                    ? (float)analysis.ConfidenceScores.Average(c => c.Score) / 100f
                    : 0f;

            var criticalMissing = analysis.ConfidenceScores
                .Where(c => c.Status == "Missing")
                .SelectMany(c => c.MissingItems)
                .Distinct()
                .Take(5)
                .ToList();

            var weakAreas = analysis.ConfidenceScores
                .Where(c => c.Status == "PartiallyCovered")
                .Select(c => $"{c.Category}: {c.Reason}")
                .Take(5)
                .ToList();

            session.CompletenessReport = new TaskPilot.AI.Models.Requirements.CompletenessReport
            {
                Score            = Math.Clamp(aiScore, 0f, 1f),
                ReadyForPlanning = analysis.RequirementCompletenessReport?.ReadyForFinalization ?? false,
                CriticalMissingAreas = criticalMissing,
                OptionalMissingAreas = new System.Collections.Generic.List<string>(),
                WeakRequirements     = weakAreas
            };

            session.AddDecision(nameof(RequirementAnalysisAgent),
                $"Replaced generic questions with {analysis.GapQuestions.Count} BRD-specific gap questions. " +
                $"Confidence scores populated for {analysis.ConfidenceScores.Count} categories. " +
                $"Overall completeness: {analysis.RequirementCompletenessReport?.OverallCompleteness}% " +
                $"(ReadyForFinalization: {analysis.RequirementCompletenessReport?.ReadyForFinalization}). " +
                $"Recommendation: {analysis.RequirementCompletenessReport?.ReadinessRecommendation}");
        }

        /// <summary>Maps the AIâ€™s string category label to the QuestionCategory enum.</summary>
        private static TaskPilot.AI.Enums.QuestionCategory MapCategory(string category) =>
            category?.Trim() switch
            {
                "BusinessGoals" => TaskPilot.AI.Enums.QuestionCategory.BusinessGoals,
                "Scale"         => TaskPilot.AI.Enums.QuestionCategory.Scale,
                "Integration"   => TaskPilot.AI.Enums.QuestionCategory.Integration,
                "Timeline"      => TaskPilot.AI.Enums.QuestionCategory.Timeline,
                "Compliance"    => TaskPilot.AI.Enums.QuestionCategory.Compliance,
                "UserRoles"     => TaskPilot.AI.Enums.QuestionCategory.UserRoles,
                "Realtime"      => TaskPilot.AI.Enums.QuestionCategory.Realtime,
                _               => TaskPilot.AI.Enums.QuestionCategory.General
            };

        /// <summary>Maps the AIâ€™s string priority label to the QuestionPriority enum.</summary>
        private static TaskPilot.AI.Enums.QuestionPriority MapPriority(string priority) =>
            priority?.Trim().ToUpperInvariant() switch
            {
                "CRITICAL" => TaskPilot.AI.Enums.QuestionPriority.Critical,
                "HIGH"     => TaskPilot.AI.Enums.QuestionPriority.High,
                "LOW"      => TaskPilot.AI.Enums.QuestionPriority.Low,
                _          => TaskPilot.AI.Enums.QuestionPriority.Medium
            };
    }
}
