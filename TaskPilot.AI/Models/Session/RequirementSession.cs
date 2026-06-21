using System.Text.Json.Serialization;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Questions;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Workflow;

namespace TaskPilot.AI.Models.Session
{
    public class RequirementSession
    {
        public Guid SessionId
        {
            get;
            set;
        }

        public RequirementSessionStatus
            Status
        {
            get;
            set;
        }

        public Guid? ProjectId
        {
            get;
            set;
        }

        // Requirements
        public ExtractedRequirements
            Requirements
        {
            get;
            set;
        }
        =
            new();

        // Question Pool
        public List<ClarificationQuestion>
            QuestionPool
        {
            get;
            set;
        }
        =
            new();

        // Conversation
        public List<ConversationMessage>
            ConversationHistory
        {
            get;
            set;
        }
        =
            new();

        // Discovery intelligence
        [JsonIgnore]
        public List<AmbiguityItem>
            DetectedAmbiguities
        {
            get;
            set;
        }
        =
            new();

        public CompletenessReport?
            CompletenessReport
        {
            get;
            set;
        }

        // Knowledge
        public SessionKnowledgeContext
            Knowledge
        {
            get;
            set;
        }
        =
            new();

        // Final output
        public StructuredRequirements?
            FinalRequirements
        {
            get;
            set;
        }

        // Workflow tracking
        public WorkflowStepResult?
            LastWorkflowResult
        {
            get;
            set;
        }

        // Audit
        [JsonIgnore]
        public List<AgentDecision>
            Decisions
        {
            get;
            set;
        }
        =
            new();

        public string?
            LastError
        {
            get;
            set;
        }

        public DateTime CreatedAt
        {
            get;
            set;
        }
        =
            DateTime.UtcNow;

        public DateTime UpdatedAt
        {
            get;
            set;
        }
        =
            DateTime.UtcNow;

        // Helpers
        public bool AllQuestionsAnswered =>
            QuestionPool.Any()
            &&
            QuestionPool.All(q =>
                q.IsAnswered);

        [JsonIgnore]
        public List<ClarificationQuestion>
            UnansweredQuestions =>
                QuestionPool
                    .Where(q =>
                        !q.IsAnswered)
                    .ToList();
    }
}
