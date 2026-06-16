using System.Text;
using TaskPilot.AI.Agents.ContextAdvisor;
using TaskPilot.AI.Models.ContextAdvisor;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.RAG;

namespace TaskPilot.AI.Orchestrators
{
    public class ContextAdvisorOrchestrator
    {
        private readonly IProjectKnowledgeSearchService _knowledgeSearchService;
        private readonly AgileCoachAgent _agileCoachAgent;
        private readonly IContextAdvisorConversationStore _conversationStore;

        public ContextAdvisorOrchestrator(
            IProjectKnowledgeSearchService knowledgeSearchService,
            AgileCoachAgent agileCoachAgent,
            IContextAdvisorConversationStore conversationStore)
        {
            _knowledgeSearchService = knowledgeSearchService;
            _agileCoachAgent = agileCoachAgent;
            _conversationStore = conversationStore;
        }

        public async Task<ContextSummaryResponse> GenerateSummaryAsync(
            TaskContextRequest request,
            CancellationToken cancellationToken = default)
        {
            var conversation =
                new ContextAdvisorConversation
                {
                    ProjectId = request.ProjectId,
                    TaskId = request.TaskId
                };

            var retrievedChunks =
                await _knowledgeSearchService
                    .SearchAsync(
                        request.ProjectId,
                        BuildSearchQuery(request),
                        request.TopK,
                        cancellationToken);

            var response =
                await _agileCoachAgent
                    .SummarizeAsync(request, retrievedChunks, cancellationToken);

            response.ConversationId = conversation.Id;

            conversation.Messages.Add(
                new ConversationMessage
                {
                    Role = "assistant",
                    Message = response.Summary
                });

            await _conversationStore
                .SaveAsync(conversation, cancellationToken);

            return response;
        }

        public async Task<ContextAdvisorAnswerResponse> AskAsync(
            ContextAdvisorChatRequest request,
            CancellationToken cancellationToken = default)
        {
            var conversation =
                request.ConversationId.HasValue
                    ? await _conversationStore
                        .GetAsync(request.ConversationId.Value, cancellationToken)
                    : null;

            conversation ??=
                new ContextAdvisorConversation
                {
                    ProjectId = request.ProjectId,
                    TaskId = request.TaskId
                };

            conversation.ProjectId ??= request.ProjectId;
            conversation.TaskId ??= request.TaskId;

            conversation.Messages.Add(
                new ConversationMessage
                {
                    Role = "user",
                    Message = request.Question
                });

            var retrievedChunks =
                await _knowledgeSearchService
                    .SearchAsync(
                        request.ProjectId,
                        BuildSearchQuery(request, request.Question),
                        request.TopK,
                        cancellationToken);

            var response =
                await _agileCoachAgent
                    .AnswerAsync(
                        request,
                        request.Question,
                        retrievedChunks,
                        conversation.Messages,
                        cancellationToken);

            response.ConversationId = conversation.Id;

            conversation.Messages.Add(
                new ConversationMessage
                {
                    Role = "assistant",
                    Message = response.Answer
                });

            await _conversationStore
                .SaveAsync(conversation, cancellationToken);

            return response;
        }

        private static string BuildSearchQuery(
            TaskContextRequest request,
            string? question = null)
        {
            var builder = new StringBuilder();

            builder.AppendLine(request.TaskTitle);
            builder.AppendLine(request.TaskDescription);
            builder.AppendLine(request.AcceptanceCriteria);
            builder.AppendLine(request.TechnicalSummary);

            foreach (var pastTask in request.RelatedPastTasks)
            {
                builder.AppendLine(pastTask);
            }

            if (!string.IsNullOrWhiteSpace(question))
            {
                builder.AppendLine(question);
            }

            return builder.ToString();
        }
    }
}
