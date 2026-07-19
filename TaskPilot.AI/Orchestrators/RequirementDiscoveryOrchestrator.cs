using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.AI.Requirements;
using TaskPilot.AI.Services.Requirements;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Models.Questions;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models;
using TaskPilot.Models.Enums;

namespace TaskPilot.AI.Orchestrators
{
    public class RequirementDiscoveryOrchestrator
    {
        private readonly IRequirementSessionStore _sessionStore;
        private readonly ILogger<RequirementDiscoveryOrchestrator> _logger;
        private readonly IEnumerable<IDocumentTextExtractor> _extractors;
        private readonly DocumentCategorizationAgent _categorizationAgent;
        private readonly ChunkingAgent _chunkingAgent;
        private readonly IDocumentStore _documentStore;
        private readonly IVectorStore _vectorStore;
        private readonly QuestionResolutionAgent _questionResolutionAgent;
        private readonly RequirementExtractionAgent _extractionAgent;
        private readonly RequirementAnalysisAgent _requirementAnalysisAgent;
        private readonly IRequirementReadinessEvaluator _readinessEvaluator;
        private readonly RequirementsBuilderAgent _builderAgent;
        private readonly RequirementValidationAgent _validationAgent;
        private readonly KnowledgeEvolutionAgent _evolutionAgent;
        private readonly TaskPilot.AI.Services.Requirements.RequirementConsolidationEngine _consolidationEngine;
        private readonly IEnumerable<IDocumentVisualExtractor> _visualExtractors;
        private readonly VisualAnalysisAgent _visualAnalysisAgent;

        public RequirementDiscoveryOrchestrator(
            IRequirementSessionStore sessionStore,
            ILogger<RequirementDiscoveryOrchestrator> logger,
            IEnumerable<IDocumentTextExtractor> extractors,
            IEnumerable<IDocumentVisualExtractor> visualExtractors,
            DocumentCategorizationAgent categorizationAgent,
            ChunkingAgent chunkingAgent,
            IDocumentStore documentStore,
            IVectorStore vectorStore,
            QuestionResolutionAgent questionResolutionAgent,
            RequirementExtractionAgent extractionAgent,
            RequirementAnalysisAgent requirementAnalysisAgent,
            IRequirementReadinessEvaluator readinessEvaluator,
            RequirementsBuilderAgent builderAgent,
            RequirementValidationAgent validationAgent,
            KnowledgeEvolutionAgent evolutionAgent,
            VisualAnalysisAgent visualAnalysisAgent,
            TaskPilot.AI.Services.Requirements.RequirementConsolidationEngine consolidationEngine)
        {
            _sessionStore = sessionStore;
            _logger = logger;
            _extractors = extractors;
            _visualExtractors = visualExtractors;
            _categorizationAgent = categorizationAgent;
            _chunkingAgent = chunkingAgent;
            _documentStore = documentStore;
            _vectorStore = vectorStore;
            _questionResolutionAgent = questionResolutionAgent;
            _extractionAgent = extractionAgent;
            _requirementAnalysisAgent = requirementAnalysisAgent;
            _readinessEvaluator = readinessEvaluator;
            _builderAgent = builderAgent;
            _validationAgent = validationAgent;
            _evolutionAgent = evolutionAgent;
            _visualAnalysisAgent = visualAnalysisAgent;
            _consolidationEngine = consolidationEngine;
        }

        public async Task<RequirementDiscoveryResponse> ExecuteAsync(RequirementDiscoveryRequest request, CancellationToken cancellationToken)
        {
            // 1. Session Resolution
            RequirementSession session;
            if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
            {
                session = await _sessionStore.GetAsync(request.SessionId.Value, cancellationToken);
                if (session == null)
                    throw new Exception("Session not found");
            }
            else
            {
                session = new RequirementSession
                {
                    SessionId = Guid.NewGuid(),
                    Status = RequirementSessionStatus.RequirementGathering,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }



            bool isModified = false;
            int documentsProcessed = 0;
            bool conversationUpdated = false;

            // 2. Persist Documents & Process
            if (request.Documents != null && request.Documents.Any())
            {
                foreach (var file in request.Documents)
                {
                    var extractor = _extractors.FirstOrDefault(e => e.CanHandle(file.ContentType, file.FileName));
                    if (extractor == null)
                    {
                        _logger.LogWarning($"No extractor found for {file.ContentType}");
                        continue;
                    }

                    using var stream = file.OpenReadStream();
                    var extractedText = await extractor.ExtractTextAsync(stream, cancellationToken);

                    var category = await _categorizationAgent.CategorizeAsync(file.FileName, extractedText, cancellationToken: cancellationToken);

                    var document = new IngestedDocument
                    {
                        Id = Guid.NewGuid(),
                        FileName = file.FileName,
                        Category = category,
                        ContentType = file.ContentType,
                        FileSize = file.Length,
                        ExtractedText = extractedText,
                        UploadedAt = DateTime.UtcNow,
                        CloudinaryUrl = string.Empty
                    };

                    var chunks = await _chunkingAgent.ChunkContentAsync(document.Id, extractedText, cancellationToken: cancellationToken);
                    document.ChunkCount = chunks.Count;

                    // --- Visual Asset Extraction & Analysis ---
                    var visualExtractors = _visualExtractors.Where(e => e.CanHandle(file.ContentType, file.FileName));
                    var extractedImages = new List<ExtractedVisualFile>();
                    foreach (var visExt in visualExtractors)
                    {
                        using var visualStream = file.OpenReadStream();
                        var imgs = await visExt.ExtractImagesAsync(visualStream, cancellationToken);
                        extractedImages.AddRange(imgs);
                    }

                    var visualRequirements = new List<RequirementIdentity>();
                    foreach (var img in extractedImages)
                    {
                        var analysisResult = await _visualAnalysisAgent.AnalyzeImageAsync(string.Empty, img.RawBytes, img.ContentType, cancellationToken);

                        var asset = new VisualAsset
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = document.Id,
                            FileName = img.FileName,
                            ContentType = img.ContentType,
                            PageNumber = img.PageNumber,
                            DiagramType = analysisResult.DiagramType,
                            Description = analysisResult.SummaryDescription,
                            ExtractedText = analysisResult.ExtractedText
                        };
                        document.VisualAssets.Add(asset);

                        foreach (var req in analysisResult.ExtractedRequirements)
                        {
                            visualRequirements.Add(new RequirementIdentity
                            {
                                OriginalText = req.Text,
                                Category = req.Category,
                                Sources = new List<string> { $"Diagram on page {img.PageNumber} of {file.FileName}" }
                            });
                        }

                        var diagramChunk = new KnowledgeChunk
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = document.Id,
                            RequirementSessionId = session.SessionId,
                            Category = DocumentCategory.Diagram,
                            SourceFile = file.FileName,
                            DocumentType = file.ContentType,
                            Content = $"Diagram Type: {analysisResult.DiagramType}\nDescription: {analysisResult.SummaryDescription}\nStructured Metadata: {analysisResult.ExtractedText}",
                            ChunkIndex = 10000 + img.PageNumber
                        };
                        chunks.Add(diagramChunk);
                    }

                    await _documentStore.SaveDocumentAsync(document, cancellationToken);
                    await _documentStore.SaveChunksAsync(chunks, cancellationToken);

                    foreach (var chunk in chunks)
                    {
                        chunk.RequirementSessionId = session.SessionId;
                        chunk.Category = chunk.Category == DocumentCategory.Diagram ? DocumentCategory.Diagram : category;
                        chunk.SourceFile = file.FileName;
                        chunk.DocumentType = file.ContentType;
                    }

                    await _vectorStore.UpsertAsync(TaskPilot.Models.Enums.KnowledgeCollectionType.ProjectPolicies, chunks, cancellationToken);

                    if (visualRequirements.Any())
                    {
                        await _consolidationEngine.ConsolidateAsync(session, visualRequirements, cancellationToken);
                    }

                    session.Knowledge.Documents.Add(document);
                    session.Knowledge.DocumentIds.Add(document.Id);
                    documentsProcessed++;
                }
                isModified = true;
            }

            // 3. Persist Conversation & Answer Questions
            if (!string.IsNullOrWhiteSpace(request.Message))
            {
                var userMessage = new ConversationMessage
                {
                    Role = "User",
                    Message = request.Message,
                    Timestamp = DateTime.UtcNow
                };
                session.ConversationHistory.Add(userMessage);

                // Evaluate Knowledge Evolution
                var evolution = await _evolutionAgent.EvaluateAsync(session, request.Message, cancellationToken);
                
                // AwaitingBrd check: If we're waiting for a BRD and get a text message with no documents.
                if (session.Status == RequirementSessionStatus.AwaitingBrd && !session.Knowledge.Documents.Any())
                {
                    if (!evolution.Intent.Equals("NoBRD", StringComparison.OrdinalIgnoreCase))
                    {
                        // Affirmative or ambiguous response (e.g. "Yes"). 
                        // Keep AwaitingBrd, do not invoke further agents, and explicitly ask for the file.
                        session.QuestionPool.RemoveAll(q => !q.IsAnswered);
                        session.QuestionPool.Add(new ClarificationQuestion
                        {
                            Id = Guid.NewGuid(),
                            Question = "Please go ahead and upload your BRD document and I'll analyze it for you.",
                            Category = TaskPilot.AI.Enums.QuestionCategory.General,
                            Priority = TaskPilot.AI.Enums.QuestionPriority.Critical,
                            Reason = "Waiting for BRD upload.",
                            IsBrdPrompt = true
                        });
                        
                        session.UpdatedAt = DateTime.UtcNow;
                        await _sessionStore.SaveAsync(session, cancellationToken);
                        return MapToResponse(session, documentsProcessed, conversationUpdated);
                    }
                    // If NoBRD, flow continues downward to activate interview mode.
                }
                
                // Always try to resolve pending questions, regardless of the primary intent,
                // because the user might provide answers alongside new requirements.
                if (session.QuestionPool.Any(q => !q.IsAnswered))
                {
                    var extractedAnswers = await _questionResolutionAgent.ResolveAsync(
                        session.QuestionPool.Where(q => !q.IsAnswered).ToList(),
                        request.Message);

                    foreach (var answer in extractedAnswers)
                    {
                        var q = session.QuestionPool.FirstOrDefault(x => x.Id == answer.QuestionId);
                        if (q != null && !q.IsAnswered && answer.IsAnswered)
                        {
                            q.IsAnswered = true;
                            q.Answer = answer.ExtractedAnswer;
                            q.AnsweredAt = DateTime.UtcNow;
                            q.AnsweredFromSource = "PM";
                        }
                    }
                }

                // Always check if we can advance the interview progress
                if (session.IsInterviewMode)
                {
                    var currentGroupQuestions = session.QuestionPool.Where(q => q.InterviewGroupIndex == session.InterviewProgress).ToList();
                    if (currentGroupQuestions.Any() && currentGroupQuestions.All(q => q.IsAnswered))
                    {
                        session.InterviewProgress++;
                    }
                }

                if (evolution.Intent.Equals("Add", StringComparison.OrdinalIgnoreCase) || evolution.Intent.Equals("Modify", StringComparison.OrdinalIgnoreCase) || evolution.Intent.Equals("Conflict", StringComparison.OrdinalIgnoreCase))
                {
                    var proposed = new RequirementIdentity
                    {
                        OriginalText = evolution.ProposedText,
                        Category = evolution.Category,
                        Sources = { "User Conversation" },
                        IsConflicting = evolution.Intent.Equals("Conflict", StringComparison.OrdinalIgnoreCase),
                        ConflictReason = evolution.Intent.Equals("Conflict", StringComparison.OrdinalIgnoreCase) ? evolution.Reasoning : string.Empty
                    };
                    await _consolidationEngine.ConsolidateAsync(session, new List<RequirementIdentity> { proposed }, cancellationToken);
                }
                else if (evolution.Intent.Equals("NoBRD", StringComparison.OrdinalIgnoreCase))
                {
                    session.IsInterviewMode = true;
                    session.InterviewProgress = 0;
                    
                    var interviewQuestions = await _requirementAnalysisAgent.GenerateInterviewQuestionsAsync(session, cancellationToken);
                    foreach (var group in interviewQuestions.QuestionGroups)
                    {
                        foreach (var q in group.Questions)
                        {
                            q.Id = Guid.NewGuid();
                            q.InterviewGroupIndex = group.GroupIndex;
                            q.InterviewTopic = group.Topic;
                            session.QuestionPool.Add(q);
                        }
                    }
                }

                // Robust fallback: If there are no documents and we haven't entered interview mode yet,
                // and we've already asked for a BRD in a prior turn,
                // any text response implicitly triggers interview mode because we cannot wait indefinitely.
                if (session.Status != RequirementSessionStatus.AwaitingBrd && !session.Knowledge.Documents.Any() && !session.IsInterviewMode && session.ConversationHistory.Count > 1)
                {
                    session.IsInterviewMode = true;
                    session.InterviewProgress = 0;

                    var brdPrompt = session.QuestionPool.FirstOrDefault(q => q.IsBrdPrompt);
                    if (brdPrompt != null && !brdPrompt.IsAnswered)
                    {
                        brdPrompt.IsAnswered = true;
                        brdPrompt.Answer = request.Message;
                        brdPrompt.AnsweredAt = DateTime.UtcNow;
                        brdPrompt.AnsweredFromSource = "System";
                    }

                    var interviewQuestions = await _requirementAnalysisAgent.GenerateInterviewQuestionsAsync(session, cancellationToken);
                    foreach (var group in interviewQuestions.QuestionGroups)
                    {
                        foreach (var q in group.Questions)
                        {
                            q.Id = Guid.NewGuid();
                            q.InterviewGroupIndex = group.GroupIndex;
                            q.InterviewTopic = group.Topic;
                            session.QuestionPool.Add(q);
                        }
                    }
                }

                conversationUpdated = true;
                isModified = true;
            }

            if (!isModified)
            {
                throw new Exception("Request must contain a Message or Documents.");
            }

            if (!session.QuestionPool.Any(q => q.IsBrdPrompt) && !session.Knowledge.Documents.Any() && !session.IsInterviewMode)
            {
                session.Status = RequirementSessionStatus.AwaitingBrd;
                session.QuestionPool.Add(new ClarificationQuestion
                    {
                        Id = Guid.NewGuid(),
                        Question = "Do you have a Business Requirements Document (BRD) you would like to upload? If yes, please attach it now. If not, just let me know and we'll get started with some questions instead.",
                        Category = TaskPilot.AI.Enums.QuestionCategory.General,
                        Priority = TaskPilot.AI.Enums.QuestionPriority.Critical,
                        Reason = "A BRD is the primary source of truth.",
                        IsBrdPrompt = true
                    });
                
                session.UpdatedAt = DateTime.UtcNow;
                await _sessionStore.SaveAsync(session, cancellationToken);
                return MapToResponse(session, documentsProcessed, conversationUpdated);
            }

            // Ensure BRD prompt is marked answered if we have documents or are in interview mode
            if (session.Knowledge.Documents.Any() || session.IsInterviewMode)
            {
                var brdPrompt = session.QuestionPool.FirstOrDefault(q => q.IsBrdPrompt);
                if (brdPrompt != null && !brdPrompt.IsAnswered)
                {
                    brdPrompt.IsAnswered = true;
                    brdPrompt.AnsweredAt = DateTime.UtcNow;
                    brdPrompt.AnsweredFromSource = "System";
                }
            }

            // 4. Requirement Analysis
            // Analyze the full BRD context combined with the conversation history.
            var analysis = await _requirementAnalysisAgent.AnalyzeAsync(session, cancellationToken);

            // 5. Update Requirements via Consolidation Engine
            var proposedExtractions = MapToIdentities(analysis.ExtractedRequirements, "BRD Analysis");
            if (proposedExtractions.Any())
            {
                await _consolidationEngine.ConsolidateAsync(session, proposedExtractions, cancellationToken);
            }
            
            // Merge Confidence Scores
            if (analysis.ConfidenceScores != null && analysis.ConfidenceScores.Any())
            {
                foreach (var incomingScore in analysis.ConfidenceScores)
                {
                    var existingScore = session.ConfidenceScores.FirstOrDefault(c => c.Category == incomingScore.Category);
                    if (existingScore != null)
                    {
                        existingScore.Score = incomingScore.Score;
                        existingScore.Status = incomingScore.Status;
                        existingScore.ExtractedValue = incomingScore.ExtractedValue;
                        existingScore.Reason = incomingScore.Reason;
                        existingScore.Evidence = incomingScore.Evidence;
                        existingScore.MissingItems = incomingScore.MissingItems;
                    }
                    else
                    {
                        session.ConfidenceScores.Add(new RequirementConfidenceScore
                        {
                            Category = incomingScore.Category,
                            Score = incomingScore.Score,
                            Status = incomingScore.Status,
                            ExtractedValue = incomingScore.ExtractedValue,
                            Reason = incomingScore.Reason,
                            Evidence = incomingScore.Evidence,
                            MissingItems = incomingScore.MissingItems
                        });
                    }
                }
            }

            // 6. Update Completeness Report
            // Always start from the deterministic evaluator — this is the authoritative score.
            var completenessReport = _readinessEvaluator.Evaluate(session);

            // Preserve the LLM's qualitative estimate for display only — do NOT use it as the numeric threshold.
            if (analysis.RequirementCompletenessReport != null &&
                analysis.RequirementCompletenessReport.EstimatedCompletenessAfterPendingQuestions > completenessReport.OverallCompleteness)
            {
                completenessReport.EstimatedCompletenessAfterPendingQuestions = analysis.RequirementCompletenessReport.EstimatedCompletenessAfterPendingQuestions;
            }

            // Score-backwards guard: never let the deterministic score decrease from a previously stored value.
            var previousScore = session.RequirementCompletenessReport?.OverallCompleteness ?? 0;
            if (completenessReport.OverallCompleteness < previousScore)
            {
                completenessReport.OverallCompleteness = previousScore;
                // Re-evaluate ReadyForFinalization with the preserved score.
                if (completenessReport.OverallCompleteness >= 85)
                {
                    completenessReport.BlockingFactors.RemoveAll(f => f.Contains("Overall completeness is"));
                }
                completenessReport.ReadyForFinalization = !completenessReport.BlockingFactors.Any();
            }

            session.RequirementCompletenessReport = completenessReport;

            // Update backward-compatible CompletenessReport using the deterministic score.
            var deterministicScoreFloat = completenessReport.OverallCompleteness / 100f;
            session.CompletenessReport = new CompletenessReport
            {
                Score = Math.Clamp(deterministicScoreFloat, 0f, 1f),
                ReadyForPlanning = completenessReport.ReadyForFinalization,
                CriticalMissingAreas = completenessReport.MissingCriticalAreas ?? new List<string>(),
                OptionalMissingAreas = new List<string>(),
                WeakRequirements = session.ConfidenceScores.Where(c => c.Status == "PartiallyCovered").Select(c => $"{c.Category}: {c.Reason}").ToList()
            };

            // 7. Regenerate Pending Questions
            if (!session.IsInterviewMode)
            {
                session.QuestionPool.RemoveAll(q => !q.IsAnswered && !q.IsBrdPrompt);
                foreach (var gapQuestion in (analysis.GapQuestions ?? new()))
                {
                    session.QuestionPool.Add(new ClarificationQuestion
                    {
                        Id = Guid.NewGuid(),
                        Question = gapQuestion.Question,
                        Category = MapCategory(gapQuestion.Category),
                        Priority = MapPriority(gapQuestion.Priority),
                        Reason = gapQuestion.Reason,
                        MissingItems = gapQuestion.MissingItems,
                        BusinessImpact = gapQuestion.BusinessImpact,
                        EstimatedEffectOnCompleteness = gapQuestion.EstimatedEffectOnCompleteness,
                        IsBrdPrompt = false
                    });
                }
            }

            // 8. Move to Planning if all questions are answered and ready for finalization.
            // Both the BRD path and interview mode now use ReadyForFinalization which is set
            // deterministically — no more LLM score override for the planning gate.
            bool isReadyForPlanning = session.AllQuestionsAnswered && session.RequirementCompletenessReport.ReadyForFinalization;

            if (isReadyForPlanning)
            {
                // Run Validation Agent
                session.ValidationResult = await _validationAgent.ValidateAsync(session, cancellationToken);
                
                // Finalization is permitted
                session.Status = RequirementSessionStatus.Planning;
                if (session.FinalRequirements == null)
                {
                    session.FinalRequirements = await _builderAgent.BuildAsync(session, cancellationToken);
                }
            }
            else
            {
                session.Status = RequirementSessionStatus.RequirementValidation;
                session.ValidationResult = null; // Clear if not ready
                
                // Clear any old stuck generic fallback questions from previous sessions so they don't block
                session.QuestionPool.RemoveAll(q => !q.IsAnswered && q.Question.Contains("85% completeness threshold"));

                // Guard against the UI incorrectly enabling finalization when pending questions are exhausted but completeness is low
                var activeQuestions = session.QuestionPool.Where(q => !q.IsAnswered && (!session.IsInterviewMode || q.InterviewGroupIndex == session.InterviewProgress));
                if (!activeQuestions.Any())
                {
                    // Use actual AI gap questions if available
                    if (analysis.GapQuestions != null && analysis.GapQuestions.Any())
                    {
                        foreach (var gapQuestion in analysis.GapQuestions)
                        {
                            session.QuestionPool.Add(new ClarificationQuestion
                            {
                                Id = Guid.NewGuid(),
                                Question = gapQuestion.Question,
                                Category = MapCategory(gapQuestion.Category),
                                Priority = MapPriority(gapQuestion.Priority),
                                Reason = gapQuestion.Reason,
                                MissingItems = gapQuestion.MissingItems,
                                BusinessImpact = gapQuestion.BusinessImpact,
                                EstimatedEffectOnCompleteness = gapQuestion.EstimatedEffectOnCompleteness,
                                IsBrdPrompt = false,
                                InterviewGroupIndex = session.IsInterviewMode ? session.InterviewProgress : 0
                            });
                        }
                    }
                    else
                    {
                        var missingStr = session.RequirementCompletenessReport.MissingCriticalAreas.Any() 
                            ? string.Join(", ", session.RequirementCompletenessReport.MissingCriticalAreas) 
                            : (session.RequirementCompletenessReport.BlockingCategories.Any() ? string.Join(", ", session.RequirementCompletenessReport.BlockingCategories) : "additional requirements");

                        session.QuestionPool.Add(new ClarificationQuestion
                        {
                            Id = Guid.NewGuid(),
                            Question = $"We need more information to reach the 85% completeness threshold. Could you provide more details regarding: {missingStr}?",
                            Category = MapCategory("General"),
                            Priority = MapPriority("High"),
                            Reason = "Completeness score is below the required 85% threshold.",
                            BusinessImpact = "Without more details, we cannot generate a reliable Work Breakdown Structure.",
                            InterviewGroupIndex = session.IsInterviewMode ? session.InterviewProgress : 0
                        });
                    }
                }
            }

            var finalPendingQuestions = session.QuestionPool.Where(q => !q.IsAnswered && (!session.IsInterviewMode || q.InterviewGroupIndex == session.InterviewProgress)).ToList();
            if (finalPendingQuestions.Any())
            {
                var combinedMessage = string.Join("\n", finalPendingQuestions.Select(q => q.Question));
                session.ConversationHistory.Add(new ConversationMessage
                {
                    Role = "Assistant",
                    Message = combinedMessage,
                    Timestamp = DateTime.UtcNow
                });
            }

            session.UpdatedAt = DateTime.UtcNow;
            await _sessionStore.SaveAsync(session, cancellationToken);

            return MapToResponse(session, documentsProcessed, conversationUpdated);
        }

        private RequirementDiscoveryResponse MapToResponse(RequirementSession session, int documentsProcessed, bool conversationUpdated)
        {
            var coverage = new CoverageDTO
            {
                Covered = session.ConfidenceScores.Where(c => c.Score >= 80).Select(c => c.Category).ToList(),
                Partial = session.ConfidenceScores.Where(c => c.Score >= 40 && c.Score < 80).Select(c => c.Category).ToList(),
                Missing = session.ConfidenceScores.Where(c => c.Score < 40).Select(c => c.Category).ToList()
            };

            var scores = session.ConfidenceScores.Select(c => c.Score).ToList();
            var confidenceSummary = new ConfidenceSummaryDTO
            {
                CoveredCategories = coverage.Covered.Count,
                PartialCategories = coverage.Partial.Count,
                MissingCategories = coverage.Missing.Count,
                AverageConfidence = scores.Any() ? scores.Sum() / scores.Count : 0,
                HighestConfidence = scores.Any() ? scores.Max() : 0,
                LowestConfidence = scores.Any() ? scores.Min() : 0
            };

            var documents = session.Knowledge.Documents.Select(d => new DocumentSummaryDTO
            {
                DocumentId = d.Id,
                FileName = d.FileName,
                UploadedAt = d.UploadedAt,
                ChunkCount = d.ChunkCount
            }).ToList();

            var analysisSummary = new AnalysisSummaryDTO
            {
                DocumentsAnalyzed = session.Knowledge.Documents.Count,
                ConversationMessages = session.ConversationHistory.Count,
                QuestionsGenerated = session.QuestionPool.Count,
                QuestionsResolved = session.QuestionPool.Count(q => q.IsAnswered)
            };

            var enrichedReqs = new List<RequirementItemDTO>();
            if (session.Requirements.BusinessRequirements != null)
                enrichedReqs.AddRange(session.Requirements.BusinessRequirements.Select(r => new RequirementItemDTO { Text = r, Category = "BusinessGoals", Confidence = scores.Any() ? scores.Sum()/scores.Count : 0 }));
            if (session.Requirements.TechnicalRequirements != null)
                enrichedReqs.AddRange(session.Requirements.TechnicalRequirements.Select(r => new RequirementItemDTO { Text = r, Category = "Technical", Confidence = scores.Any() ? scores.Sum()/scores.Count : 0 }));
            if (session.Requirements.Constraints != null)
                enrichedReqs.AddRange(session.Requirements.Constraints.Select(r => new RequirementItemDTO { Text = r, Category = "Constraints", Confidence = scores.Any() ? scores.Sum()/scores.Count : 0 }));
            if (session.Requirements.Integrations != null)
                enrichedReqs.AddRange(session.Requirements.Integrations.Select(r => new RequirementItemDTO { Text = r, Category = "Integrations", Confidence = scores.Any() ? scores.Sum()/scores.Count : 0 }));
            if (session.Requirements.ScaleRequirements != null)
                enrichedReqs.AddRange(session.Requirements.ScaleRequirements.Select(r => new RequirementItemDTO { Text = r, Category = "Scale", Confidence = scores.Any() ? scores.Sum()/scores.Count : 0 }));

            var response = new RequirementDiscoveryResponse
            {
                SessionId = session.SessionId,
                WorkflowState = session.Status == RequirementSessionStatus.RequirementValidation ? "ClarificationRequired" : session.Status.ToString(),
                DocumentsProcessed = documentsProcessed,
                ConversationUpdated = conversationUpdated,
                NextRecommendedAction = GetRecommendedAction(session),
                Warnings = new List<string>(),
                Coverage = coverage,
                ConfidenceSummary = confidenceSummary,
                Documents = documents,
                AnalysisSummary = analysisSummary,
                Requirements = new ExtractedRequirementsDTO
                {
                    BusinessRequirements = session.Requirements.BusinessRequirements ?? new List<string>(),
                    TechnicalRequirements = session.Requirements.TechnicalRequirements ?? new List<string>(),
                    Constraints = session.Requirements.Constraints ?? new List<string>(),
                    Integrations = session.Requirements.Integrations ?? new List<string>(),
                    ScaleRequirements = session.Requirements.ScaleRequirements ?? new List<string>(),
                    EnrichedRequirements = enrichedReqs
                },
                ValidationResult = session.ValidationResult != null ? new RequirementValidationResultDTO
                {
                    ValidationScore = session.ValidationResult.ValidationScore,
                    Issues = session.ValidationResult.Issues ?? new List<string>(),
                    Warnings = session.ValidationResult.Warnings ?? new List<string>(),
                    BusinessReadiness = session.ValidationResult.BusinessReadiness
                } : null,
                PendingQuestions = session.QuestionPool.Where(q => !q.IsAnswered && (!session.IsInterviewMode || q.InterviewGroupIndex == session.InterviewProgress)).Select(q => new ClarificationQuestionDTO
                {
                    Id = q.Id,
                    Question = q.Question,
                    Category = q.Category.ToString(),
                    Priority = q.Priority.ToString(),
                    Reason = q.Reason,
                    MissingItems = q.MissingItems,
                    BusinessImpact = q.BusinessImpact,
                    EstimatedEffectOnCompleteness = q.EstimatedEffectOnCompleteness
                }).ToList()
            };

            if (session.RequirementCompletenessReport != null)
            {
                response.CompletenessReport = new RequirementCompletenessDTO
                {
                    OverallCompleteness = session.RequirementCompletenessReport.OverallCompleteness,
                    Readiness = new ReadinessDTO { Status = session.RequirementCompletenessReport.Readiness },
                    BlockingCategories = session.RequirementCompletenessReport.BlockingCategories,
                    QuestionImpact = new QuestionImpactDTO
                    {
                        HighPriorityQuestions = session.RequirementCompletenessReport.HighPriorityQuestions,
                        MediumPriorityQuestions = session.RequirementCompletenessReport.MediumQuestions,
                        LowPriorityQuestions = session.RequirementCompletenessReport.LowQuestions
                    },
                    MissingCriticalAreas = session.RequirementCompletenessReport.MissingCriticalAreas,
                    ReadinessRecommendation = session.RequirementCompletenessReport.ReadinessRecommendation,
                    BlockingFactors = session.RequirementCompletenessReport.BlockingFactors.Select(f => new BlockingFactorsDTO { Factor = f }).ToList(),
                    EstimatedCompletenessAfterPendingQuestions = session.RequirementCompletenessReport.EstimatedCompletenessAfterPendingQuestions,
                    ReadyForFinalization = session.RequirementCompletenessReport.ReadyForFinalization
                };
            }

            return response;
        }

        private string GetRecommendedAction(RequirementSession session)
        {
            if (session.RequirementCompletenessReport?.ReadyForFinalization == true && session.AllQuestionsAnswered)
            {
                return "Finalize requirements";
            }
            if (session.QuestionPool.Any(q => !q.IsAnswered))
            {
                return "Answer pending questions";
            }
            if (session.RequirementCompletenessReport != null && !session.RequirementCompletenessReport.ReadyForFinalization)
            {
                return "Upload another BRD";
            }
            return "Wait for further instructions";
        }

        private static QuestionCategory MapCategory(string category) =>
            category?.Trim() switch
            {
                "BusinessGoals" => QuestionCategory.BusinessGoals,
                "Scale"         => QuestionCategory.Scale,
                "Integration"   => QuestionCategory.Integration,
                "Timeline"      => QuestionCategory.Timeline,
                "Compliance"    => QuestionCategory.Compliance,
                "UserRoles"     => QuestionCategory.UserRoles,
                "Realtime"      => QuestionCategory.Realtime,
                _               => QuestionCategory.General
            };

        private static QuestionPriority MapPriority(string priority) =>
            priority?.Trim().ToUpperInvariant() switch
            {
                "CRITICAL" => QuestionPriority.Critical,
                "HIGH"     => QuestionPriority.High,
                "LOW"      => QuestionPriority.Low,
                _          => QuestionPriority.Medium
            };

        private List<RequirementIdentity> MapToIdentities(ExtractedRequirements reqs, string source)
        {
            var list = new List<RequirementIdentity>();
            if (reqs == null) return list;

            AddIdentities(list, reqs.BusinessRequirements, "BusinessGoals", source);
            AddIdentities(list, reqs.TechnicalRequirements, "Technical", source);
            AddIdentities(list, reqs.Constraints, "Constraints", source);
            AddIdentities(list, reqs.Integrations, "Integrations", source);
            AddIdentities(list, reqs.ScaleRequirements, "Scale", source);

            return list;
        }

        private void AddIdentities(List<RequirementIdentity> list, List<string> items, string category, string source)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                list.Add(new RequirementIdentity
                {
                    OriginalText = item,
                    Category = category,
                    Sources = { source }
                });
            }
        }
    }
}
