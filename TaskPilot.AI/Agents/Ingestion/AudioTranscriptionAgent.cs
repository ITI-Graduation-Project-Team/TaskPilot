namespace TaskPilot.AI.Agents.Ingestion
{
    public class AudioTranscriptionAgent
    {
        public Task<string> TranscribeAsync(
            Stream audioStream,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            // Clean mock architecture for audio transcription
            return Task.FromResult("Transcribed requirements discussion notes: We need user authentication via JWT, stripe integration for scale payment processing, and realtime alerts when transaction fails.");
        }
    }
}
