using System.ComponentModel.DataAnnotations;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Backlog
{
    public class CreateUserStoryDto
    {
        [Required]
        public string TitleEn { get; set; } = string.Empty;
        public string? TitleAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        [Required]
        public StoryPriority Priority { get; set; }
    }
}
