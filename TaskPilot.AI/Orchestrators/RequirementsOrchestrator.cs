using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Models.Workflow;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Models.Questions;

namespace TaskPilot.AI.Orchestrators
{
    public class RequirementsOrchestrator
    {
        private const float
            COMPLETENESS_THRESHOLD = 0.85f;

        private readonly
            InputProcessingAgent
                _inputProcessingAgent;

        private readonly
            RequirementExtractionAgent
                _extractionAgent;

        private readonly
            AmbiguityDetectionAgent
                _ambiguityAgent;

        private readonly
            CompletenessEvaluatorAgent
                _evaluatorAgent;

        private readonly
            ClarificationAgent
                _clarificationAgent;

        private readonly
            RequirementsBuilderAgent
                _builderAgent;
        private readonly
            QuestionResolutionAgent
                _questionResolutionAgent;

        private readonly RequirementAnalysisAgent _requirementAnalysisAgent;

        private readonly
            IRequirementSessionStore
                _sessionStore;

        public RequirementsOrchestrator(
            InputProcessingAgent
                inputProcessingAgent,

            RequirementExtractionAgent
                extractionAgent,

            AmbiguityDetectionAgent
                ambiguityAgent,

            CompletenessEvaluatorAgent
                evaluatorAgent,

            ClarificationAgent
                clarificationAgent,

            RequirementsBuilderAgent
                builderAgent,
            QuestionResolutionAgent
                 resolutionAgent,

            RequirementAnalysisAgent requirementAnalysisAgent,

            IRequirementSessionStore
                sessionStore)
            {
            _inputProcessingAgent =
                inputProcessingAgent;

            _extractionAgent =
                extractionAgent;

            _ambiguityAgent =
                ambiguityAgent;

            _evaluatorAgent =
                evaluatorAgent;

            _clarificationAgent =
                clarificationAgent;

            _builderAgent =
                builderAgent;
            _questionResolutionAgent =
                resolutionAgent;

            _requirementAnalysisAgent = requirementAnalysisAgent;

            _sessionStore =
                sessionStore;
        }

        public async Task<
            RequirementSession>
                StartAsync(
                    string initialInput,
                    CancellationToken
                        cancellationToken =
                            default)
        {
            var session =
                     new RequirementSession
                     {
                         SessionId =
                             Guid.NewGuid()
                     };

            UpdateStatus(
                session,
                RequirementSessionStatus
                    .RequirementGathering);

            try
            {
                // Save initial user message
                session
                    .ConversationHistory
                    .Add(
                        new ConversationMessage
                        {
                            Role = "user",

                            Message =
                                initialInput
                        });

                // Process input
                var cleanedInput =
                    await _inputProcessingAgent
                        .ProcessAsync(
                            initialInput);

                session.AddDecision(
                    nameof(InputProcessingAgent),
                    "Input normalized and preprocessed");

                // Extract requirements
                var extracted =
                    await _extractionAgent
                        .ExtractAsync(
                            cleanedInput);

                session
                 .Requirements
                 .MergeFrom(
                     extracted);

                // Inject BRD upload prompt as first assistant message (guides PM without blocking)
                session.ConversationHistory.Add(new ConversationMessage
                {
                    Role = "assistant",
                    Message =
                        "Thank you for sharing your project idea. " +
                        "To generate the most accurate Work Breakdown Structure, " +
                        "I recommend uploading your Business Requirement Document " +
                        "(BRD, SRS, RFP, or any specification) if available. " +
                        "You can upload it using the document upload option. " +
                        "If you don't have one, I'll ask you a few targeted questions instead."
                });

                // Add BRD prompt as a resolvable question
                session.QuestionPool.Add(new ClarificationQuestion
                {
                    Id        = Guid.NewGuid(),
                    Question  = "Please upload your requirement document (BRD/SRS/RFP) if available.",
                    Category  = QuestionCategory.General,
                    Priority  = QuestionPriority.High,
                    IsBrdPrompt = true
                });

                // Continue workflow
                var workflowResult =
                await ContinueAsync(
                    session,
                    cancellationToken);

                session.LastWorkflowResult =
                    workflowResult;

                return session;
            }
            catch (Exception ex)
            {
                session.LastError =
                    ex.Message;

                UpdateStatus(
                     session,
                     RequirementSessionStatus
                         .Failed);

                return session;
            }
            finally
            {
                await _sessionStore
                    .SaveAsync(
                        session,
                        cancellationToken);
            }
        }

        public async Task<
            RequirementSession>
                ProcessPMResponseAsync(
                    Guid sessionId,
                    string pmResponse,
                    CancellationToken
                        cancellationToken =
                            default)
        {
            var session =
               await _sessionStore
                .GetAsync(
                    sessionId,
                    cancellationToken);

            if (session is null)
            {
                throw new Exception(
                    "Session not found.");
            }

            try
            {
                // Save PM response
                session
                    .ConversationHistory
                    .Add(
                        new ConversationMessage
                        {
                            Role = "user",

                            Message =
                                pmResponse
                        });

                // Mark questions answered in QuestionPool
                // Use full conversation history so previously-answered questions are not re-surfaced
                var fullConversation = string.Join(
                    "\n---\n",
                    session.ConversationHistory
                           .Where(m => m.Role == "user")
                           .Select(m => m.Message));

                var resolvedQuestions =
                  await _questionResolutionAgent
                .ResolveAsync(
                    session
                        .QuestionPool
                        .Where(q =>
                            !q.IsAnswered)
                        .ToList(),

                    fullConversation);

                session.AddDecision(
                    nameof(QuestionResolutionAgent),
                    $"Resolved {resolvedQuestions.Count} questions");

                foreach (var resolution
         in resolvedQuestions)
                {
                    var question =
                        session
                            .QuestionPool
                            .FirstOrDefault(q =>

                                q.Id ==
                                    resolution.QuestionId);

                    if (question is null)
                        continue;

                    if (resolution.IsAnswered)
                    {
                        question.IsAnswered =
                            true;

                        question.AnsweredAt =
                            DateTime.UtcNow;

                        question.Answer =
                            resolution
                                .ExtractedAnswer;
                    }
                }

                // Auto-dismiss BRD prompt if PM has sent 2+ text responses (chose to continue without BRD)
                var brdPrompt = session.QuestionPool
                    .FirstOrDefault(q => q.IsBrdPrompt && !q.IsAnswered);

                if (brdPrompt is not null)
                {
                    var userMessageCount = session.ConversationHistory.Count(m => m.Role == "user");
                    if (userMessageCount >= 2)
                    {
                        brdPrompt.IsAnswered         = true;
                        brdPrompt.Answer             = "No document uploaded — continuing with conversation only.";
                        brdPrompt.AnsweredFromSource = "AutoDismissed";
                        brdPrompt.AnsweredAt         = DateTime.UtcNow;
                        session.IsLimitedMode        = true;

                        session.AddDecision("Workflow",
                            "BRD prompt auto-dismissed after 2 text responses. IsLimitedMode = true.");
                    }
                }

                // Process response
                var cleanedResponse =
                    await _inputProcessingAgent
                        .ProcessAsync(
                            pmResponse);

                session.AddDecision(
                    nameof(InputProcessingAgent),
                    "Input normalized and preprocessed");

                // Extract requirements
                var extracted =
                    await _extractionAgent
                        .ExtractAsync(
                            cleanedResponse,
                            session
                                .Requirements);

               session
              .Requirements
              .MergeFrom(
                  extracted);

                UpdateStatus(
                    session,
                    RequirementSessionStatus
                        .RequirementGathering);

                // Continue workflow
                var workflowResult =
                 await ContinueAsync(
                     session,
                     cancellationToken);

                session.LastWorkflowResult =
                    workflowResult;

                return session;
            }
            catch (Exception ex)
            {
                session.LastError =
                    ex.Message;

                UpdateStatus(
                     session,
                     RequirementSessionStatus
                         .Failed);

                return session;
            }
            finally
            {
                await _sessionStore
                    .SaveAsync(
                        session,
                        cancellationToken);
            }
        }

        private async Task<TaskPilot.AI.Models.Workflow.WorkflowStepResult>
        ContinueAsync(
            RequirementSession session,
            CancellationToken cancellationToken)
        {
            var workflowResult =
                new TaskPilot.AI.Models.Workflow.WorkflowStepResult
                {
                    Success = true
                };

            // Detect ambiguities
            await DetectAmbiguitiesAsync(session, cancellationToken);
            workflowResult.ActionsExecuted.Add("AmbiguityDetection");

            // Evaluate completeness
            await EvaluateCompletenessAsync(session, cancellationToken);
            workflowResult.ActionsExecuted.Add("CompletenessEvaluation");

            // Generate question pool ONCE
            if (!session.QuestionPool.Any())
            {
                await GenerateQuestionPoolAsync(session, cancellationToken);
                workflowResult.ActionsExecuted.Add("ClarificationGeneration");
            }

            // Move to Planning if all questions are answered
            if (session.AllQuestionsAnswered)
            {
                await MoveToPlanningAsync(session, cancellationToken);
                workflowResult.ActionsExecuted.Add("RequirementsBuilding");

                workflowResult.CurrentStage = "Planning";
                workflowResult.ReadyForNextStage = true;
            }
            else
            {
                UpdateStatus(
                    session,
                    RequirementSessionStatus.RequirementValidation);

                workflowResult.CurrentStage = "RequirementValidation";
                workflowResult.ReadyForNextStage = false;
            }

            return workflowResult;
        }

        private async Task DetectAmbiguitiesAsync(
            RequirementSession session,
            CancellationToken cancellationToken)
        {
            session.DetectedAmbiguities =
               await _ambiguityAgent
                    .DetectAsync(
                        session.Requirements);

            session.AddDecision(
                nameof(AmbiguityDetectionAgent),
                $"Detected {session.DetectedAmbiguities.Count} ambiguities");
        }

        private async Task EvaluateCompletenessAsync(
            RequirementSession session,
            CancellationToken cancellationToken)
        {
            if (session.Knowledge.Documents.Any())
            {
                var analysis = await _requirementAnalysisAgent.AnalyzeAsync(session, cancellationToken);
                session.RequirementCompletenessReport = analysis.RequirementCompletenessReport;

                var aiScore = analysis.RequirementCompletenessReport?.OverallCompleteness > 0
                    ? analysis.RequirementCompletenessReport.OverallCompleteness / 100f
                    : analysis.ConfidenceScores.Count > 0
                        ? (float)analysis.ConfidenceScores.Average(c => c.Score) / 100f
                        : 0f;

                session.CompletenessReport = new CompletenessReport
                {
                    Score = Math.Clamp(aiScore, 0f, 1f),
                    ReadyForPlanning = analysis.RequirementCompletenessReport?.ReadyForFinalization ?? false,
                    CriticalMissingAreas = analysis.RequirementCompletenessReport?.MissingCriticalAreas ?? new List<string>(),
                    OptionalMissingAreas = new List<string>(),
                    WeakRequirements = analysis.ConfidenceScores.Where(c => c.Status == "PartiallyCovered").Select(c => $"{c.Category}: {c.Reason}").ToList()
                };

                session.ConfidenceScores = analysis.ConfidenceScores.Select(c => new RequirementConfidenceScore
                {
                    Category = c.Category,
                    Score = c.Score,
                    Status = c.Status,
                    ExtractedValue = c.ExtractedValue,
                    Reason = c.Reason,
                    Evidence = c.Evidence,
                    MissingItems = c.MissingItems
                }).ToList();

                session.QuestionPool.RemoveAll(q => !q.IsAnswered && !q.IsBrdPrompt);
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
                        IsBrdPrompt = false
                    });
                }
                
                session.AddDecision(nameof(RequirementAnalysisAgent),
                    $"Re-evaluated BRD with conversation history. Completeness: {aiScore:P0}");
            }
            else
            {
                var report =
                    await _evaluatorAgent
                        .EvaluateAsync(
                            session);

                // Never let the score go backwards — LLM evaluations can fluctuate
                var previousScore = session.CompletenessReport?.Score ?? 0f;
                if (report.Score >= previousScore)
                {
                    session.CompletenessReport = report;
                }
                else
                {
                    // Keep the previous report but update diagnostic fields
                    if (session.CompletenessReport != null)
                    {
                        session.CompletenessReport.CriticalMissingAreas = report.CriticalMissingAreas;
                        session.CompletenessReport.OptionalMissingAreas = report.OptionalMissingAreas;
                        session.CompletenessReport.WeakRequirements = report.WeakRequirements;
                    }
                }

                session.AddDecision(
                    nameof(CompletenessEvaluatorAgent),
                    $"Completeness score evaluated at {report.Score} (kept: {session.CompletenessReport?.Score})");
            }
        }

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

        private static TaskPilot.AI.Enums.QuestionPriority MapPriority(string priority) =>
            priority?.Trim().ToUpperInvariant() switch
            {
                "CRITICAL" => TaskPilot.AI.Enums.QuestionPriority.Critical,
                "HIGH"     => TaskPilot.AI.Enums.QuestionPriority.High,
                "LOW"      => TaskPilot.AI.Enums.QuestionPriority.Low,
                _          => TaskPilot.AI.Enums.QuestionPriority.Medium
            };

        private async Task GenerateQuestionPoolAsync(
            RequirementSession session,
            CancellationToken cancellationToken)
        {
            var questions =
                await _clarificationAgent
                    .GenerateAsync(
                        session);

            session.QuestionPool.AddRange(questions);

            // Save AI questions in history
            session.ConversationHistory.Add(
                new ConversationMessage
                {
                    Role = "assistant",
                    Message = $"Generated {questions.Count} clarification question(s). See QuestionPool for details."
                });
        }

        private async Task MoveToPlanningAsync(
            RequirementSession session,
            CancellationToken cancellationToken)
        {
            session.FinalRequirements =
                await _builderAgent
                    .BuildAsync(
                        session);

            UpdateStatus(
                session,
                RequirementSessionStatus
                    .Planning);
        }

        private static void
        UpdateStatus(
            RequirementSession session,
            RequirementSessionStatus status)
        {
            session.Status =
                status;

            session.UpdatedAt =
                DateTime.UtcNow;

            session.AddDecision(
                "Workflow",
                $"Session moved to {status}");
        }

        public async Task<RequirementSession> StartWithDocumentAsync(
            CancellationToken cancellationToken = default)
        {
            var session = new RequirementSession { SessionId = Guid.NewGuid() };
            UpdateStatus(session, RequirementSessionStatus.RequirementGathering);

            session.AddDecision("Workflow", "Session created via Document-First entry point.");

            await _sessionStore.SaveAsync(session, cancellationToken);
            return session;
        }
    }
}