namespace TaskPilot.AI.Models.Ingestion
{
    public class AudioIngestionResult
    {
        public bool Success { get; set; }

        public Guid TranscriptId { get; set; }

        public string Transcript { get; set; }
            = string.Empty;

        public bool PendingPMReview { get; set; }
            = true;

        public string Message { get; set; }
            = string.Empty;
    }
}
