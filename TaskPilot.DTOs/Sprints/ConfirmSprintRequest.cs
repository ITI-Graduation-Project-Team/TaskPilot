using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Sprints
{
    public class ConfirmSprintRequest
    {
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public string? SprintGoalEn { get; set; }
        public string? SprintGoalAr { get; set; }

        /// <summary>
        /// Optional. If not provided, defaults to today (UTC date).
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// Optional. If not provided, defaults to StartDate + Project.SprintDurationInDays.
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Final list of UserStory IDs to include in this Sprint — this is
        /// the PM's decision after reviewing/editing the AI suggestion.
        /// Must not be empty.
        /// </summary>
        public List<Guid> UserStoryIds { get; set; } = new();
    }
}
