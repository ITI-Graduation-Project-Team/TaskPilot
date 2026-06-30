namespace TaskPilot.Models.Common.Errors
{
    public static class CvErrors
    {
        public static readonly Error ExtractionFailed = new("CV_EXTRACTION_FAILED", ErrorType.Failure);
        public static readonly Error InvalidFile = new("INVALID_CV_FILE", ErrorType.Validation);
        public static readonly Error FileTooLarge = new("CV_FILE_TOO_LARGE", ErrorType.Validation);
        public static readonly Error UnsupportedFormat = new("CV_UNSUPPORTED_FORMAT", ErrorType.Validation);
    }
}
