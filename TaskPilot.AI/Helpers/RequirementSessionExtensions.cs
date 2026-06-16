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
            return string.Join("\n", session.ConversationHistory
                .Where(m => m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Message));
        }
    }
}