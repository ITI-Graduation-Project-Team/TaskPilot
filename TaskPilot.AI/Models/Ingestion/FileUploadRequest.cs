namespace TaskPilot.AI.Models.Ingestion
{
    public class FileUploadRequest
    {
        public Guid ProjectId { get; set; }

        public string FileName { get; set; }
            = string.Empty;

        public string ContentType { get; set; }
            = string.Empty;

        public Stream FileStream { get; set; }
            = null!;

        public long FileSize { get; set; }
    }
}
