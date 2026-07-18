using System;
using System.Collections.Generic;
using TaskPilot.AI.Enums;

namespace TaskPilot.AI.Models.Questions
{
    public class ClarificationQuestion
    {
        public Guid Id
        {
            get;
            set;
        }
        =
            Guid.NewGuid();

        public string Question
        {
            get;
            set;
        }
        =
            string.Empty;

        public QuestionCategory
        Category
        {
            get;
            set;
        }
        =
          QuestionCategory
        .General;

        public QuestionPriority
            Priority
        {
            get;
            set;
        }
        =
            QuestionPriority
                .Medium;

        public bool IsAnswered
        {
            get;
            set;
        }

        public string?
            Answer
        {
            get;
            set;
        }

        public DateTime?
            AnsweredAt
        {
            get;
            set;
        }

        public string?
            AnsweredFromSource
        {
            get;
            set;
        }

        public bool IsBrdPrompt
        {
            get;
            set;
        } = false;

        // ── Enriched metadata (additive) ────────────────────────────────────

        /// <summary>
        /// Why this question matters in business terms.
        /// E.g. "Milestones are required for sprint planning and WBS generation."
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Specific BRD sub-items that are missing and that this question targets.
        /// E.g. ["Milestones", "Project Phases", "Sprint Schedule"]
        /// </summary>
        public List<string> MissingItems { get; set; } = new();

        /// <summary>
        /// Plain-English risk statement if this question is left unanswered.
        /// E.g. "Without milestones, sprint planning cannot be grounded in commitments."
        /// </summary>
        public string BusinessImpact { get; set; } = string.Empty;

        /// <summary>
        /// Estimated improvement (0–100 int) to the overall completeness score
        /// that answering this question would produce.
        /// </summary>
        public int EstimatedEffectOnCompleteness { get; set; }

        public int InterviewGroupIndex { get; set; } = 0;
        public string InterviewTopic { get; set; } = string.Empty;
    }
}
