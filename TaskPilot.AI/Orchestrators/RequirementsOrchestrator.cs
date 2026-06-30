using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Models.Workflow;
using TaskPilot.AI.Persistence.Interfaces;

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
                var resolvedQuestions =
                  await _questionResolutionAgent
                .ResolveAsync(
                    session
                        .QuestionPool
                        .Where(q =>
                            !q.IsAnswered)
                        .ToList(),

                    pmResponse);

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
                workflowResult.BlockingIssues = session.CompletenessReport?
                    .CriticalMissingAreas
                    .ToList()
                    ?? new List<string>();
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
            var report =
                await _evaluatorAgent
                    .EvaluateAsync(
                        session);

            session.CompletenessReport =
                report;

            session.AddDecision(
                nameof(CompletenessEvaluatorAgent),
                $"Completeness score evaluated at {report.Score}");
        }

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
            foreach (var question in questions)
            {
                session.ConversationHistory.Add(
                    new ConversationMessage
                    {
                        Role = "assistant",
                        Message = question.Question
                    });
            }
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
    }
}