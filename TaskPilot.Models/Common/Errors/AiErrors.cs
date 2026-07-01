namespace TaskPilot.Models.Common.Errors
{
    public static class AiErrors
    {
        public static readonly Error AiResponseUnreadable = new("AI_UNREADABLE_RESPONSE", ErrorType.Failure);
        public static readonly Error ParsingFailed = new("AI_PARSING_FAILED", ErrorType.Failure);
        public static readonly Error ServerError = new("AI_SERVER_ERROR", ErrorType.Failure);
        public static readonly Error InvalidAudioFormat = new("AI_INVALID_AUDIO_FORMAT", ErrorType.Validation);
        public static readonly Error EmptyAudio = new("AI_EMPTY_AUDIO", ErrorType.Validation);
        public static readonly Error EmptyTranscription = new("AI_EMPTY_TRANSCRIPTION", ErrorType.Validation);
        public static readonly Error ProjectNameRequired = new("AI_PROJECT_NAME_REQUIRED", ErrorType.Validation);
    }
}
