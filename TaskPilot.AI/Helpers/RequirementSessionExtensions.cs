using System;
using System.Linq;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Models.Workflow;

namespace TaskPilot.AI.Helpers
{
    public static class RequirementSessionExtensions
    {
        public static void AddDecision(
            this RequirementSession session,
            string agent,
            string decision)
        {
            session
                .Decisions
                .Add(
                    new AgentDecision
                    {
                        AgentName =
                            agent,

                        Decision =
                            decision,

                        Timestamp =
                            DateTime.UtcNow
                    });
        }

        public static string GetUserMessagesAsText(this RequirementSession session)
        {
            var userMessages = session.ConversationHistory
                .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Message)
                .ToList();

            var documentAnswers = session.QuestionPool
                .Where(q => q.IsAnswered && q.AnsweredFromSource == "Document")
                .Select(q => $"Document provided answer to '{q.Question}': {q.Answer}");

            userMessages.AddRange(documentAnswers);

            return string.Join("\n", userMessages);
        }
    }
}