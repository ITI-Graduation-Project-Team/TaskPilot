using System;

namespace TaskPilot.DTOs.Tasks.Attachments
{
    public class TaskAttachmentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FileUrl { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime UploadedAt { get; set; }

        public Guid? UploaderId { get; set; }
        public string UploaderNameEn { get; set; } = string.Empty;
        public string UploaderNameAr { get; set; } = string.Empty;
        public string UploaderRole { get; set; } = string.Empty; // "Employee" or "ProjectManager"
    }
}
