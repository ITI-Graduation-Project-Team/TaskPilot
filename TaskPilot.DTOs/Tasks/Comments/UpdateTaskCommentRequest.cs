using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.Tasks.Comments
{
    public class UpdateTaskCommentRequest
    {
        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
}
