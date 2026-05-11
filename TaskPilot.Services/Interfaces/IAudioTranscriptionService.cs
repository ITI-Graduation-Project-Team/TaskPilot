using Microsoft.AspNetCore.Http;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    /// <summary>
    /// Transcribes an uploaded audio file to text using OpenAI Whisper.
    /// </summary>
    public interface IAudioTranscriptionService
    {
        /// <summary>
        /// Sends the audio file to OpenAI Whisper and returns the transcribed text.
        /// Supported formats: .mp3, .wav, .m4a, .webm.
        /// </summary>
        /// <param name="audioFile">The uploaded audio file.</param>
        Task<Result<string>> TranscribeAsync(IFormFile audioFile);
    }
}
