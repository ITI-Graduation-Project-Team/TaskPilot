using System.ComponentModel.DataAnnotations;

namespace TaskPilot.DTOs.Tasks.Comments
{
    public class AddTaskCommentRequest
    {
        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty;
    }
}
