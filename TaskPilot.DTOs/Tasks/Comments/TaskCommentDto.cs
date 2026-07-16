using System;

namespace TaskPilot.DTOs.Tasks.Comments
{
    public class TaskCommentDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public Guid AuthorId { get; set; }
        public string AuthorNameEn { get; set; } = string.Empty;
        public string AuthorNameAr { get; set; } = string.Empty;
        public string AuthorRole { get; set; } = string.Empty; // "Employee" or "ProjectManager"
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
