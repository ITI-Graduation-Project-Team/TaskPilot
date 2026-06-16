using TaskPilot.AI.Enums;

namespace TaskPilot.AI.Models.Ingestion
{
    public class IngestedDocument
    {
        public Guid Id { get; set; }

        public Guid? ProjectId { get; set; }

        public string FileName { get; set; }
            = string.Empty;

        public DocumentCategory
             Category
                {
                    get;
                    set;
                }
         =
             DocumentCategory
                 .Uncategorized;

        public string ContentType { get; set; }
            = string.Empty;

        public long FileSize { get; set; }

        public string CloudinaryUrl { get; set; }
            = string.Empty;

        public string ExtractedText { get; set; }
            = string.Empty;

        public bool IsAvailableToContextSummarizer { get; set; }
            = true;

        public DateTime UploadedAt { get; set; }
            = DateTime.UtcNow;
    }
}
