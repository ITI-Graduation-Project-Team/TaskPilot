using System.Collections.Generic;
using TaskPilot.AI.Models.Questions;

namespace TaskPilot.AI.Models.Requirements
{
    public class InterviewQuestionGenerationResult
    {
        public List<InterviewQuestionGroup> QuestionGroups { get; set; } = new();
    }

    public class InterviewQuestionGroup
    {
        public int GroupIndex { get; set; }
        public string Topic { get; set; } = string.Empty;
        public List<ClarificationQuestion> Questions { get; set; } = new();
    }
}
