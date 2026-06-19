using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using OpenAI.Audio;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.AiProjectGenerator
{
    /// <summary>
    /// Transcribes audio files to text using the OpenAI Whisper API.
    /// </summary>
    public class AudioTranscriptionService : IAudioTranscriptionService
    {
        private static readonly HashSet<string> _supportedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".m4a", ".webm" };

        private readonly AudioClient _client;

        public AudioTranscriptionService(IConfiguration configuration)
        {
            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");

            _client = new AudioClient(model: "whisper-1", apiKey: apiKey);
        }

        /// <inheritdoc />
        public async Task<Result<string>> TranscribeAsync(IFormFile audioFile)
        {
            if (audioFile is null || audioFile.Length == 0)
                return Result.Failure<string>(AiErrors.EmptyAudio);

            var extension = Path.GetExtension(audioFile.FileName);

            if (!_supportedExtensions.Contains(extension))
                return Result.Failure<string>(
                    AiErrors.InvalidAudioFormat);

            using var stream = audioFile.OpenReadStream();

            var response = await _client.TranscribeAudioAsync(
                stream,
                audioFile.FileName);

            var transcript = response.Value.Text;

            if (string.IsNullOrWhiteSpace(transcript))
                return Result.Failure<string>(AiErrors.EmptyTranscription);

            return Result.Success(transcript);
        }
    }
}
