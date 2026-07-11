using System.ComponentModel.DataAnnotations;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Backlog
{
    public class UpdateTaskDto
    {
        [Required]
        public string TitleEn { get; set; } = string.Empty;
        public string? TitleAr { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public string? TechnicalSummaryEn { get; set; }
        public string? TechnicalSummaryAr { get; set; }
        public string? AcceptanceCriteriaEn { get; set; }
        public string? AcceptanceCriteriaAr { get; set; }
        
        [Required]
        public TaskPriority Priority { get; set; }
        
        [Range(0.1, 1000, ErrorMessage = "EstimatedHours must be greater than 0.")]
        public decimal EstimatedHours { get; set; }
        
        [Required]
        public EffortSize EffortSize { get; set; }
        
        [Required]
        public TaskType Type { get; set; }

        [Required]
        public TaskItemStatus Status { get; set; }
    }
}
