using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Models.Common.Errors
{
    public static class TaskAttachmentErrors
    {
        public static readonly Error AttachmentNotFound = new(
            "TASK_ATTACHMENT_NOT_FOUND",
            ErrorType.NotFound,
            "The requested task attachment was not found.");

        public static readonly Error AttachmentForbidden = new(
            "TASK_ATTACHMENT_FORBIDDEN",
            ErrorType.Forbidden,
            "You are not authorized to perform this action on the attachment.");

        public static readonly Error InvalidFile = new(
            "TASK_ATTACHMENT_INVALID_FILE",
            ErrorType.Validation,
            "The uploaded file is empty or invalid.");

        public static readonly Error FileTooLarge = new(
            "TASK_ATTACHMENT_FILE_TOO_LARGE",
            ErrorType.Validation,
            "The uploaded file exceeds the 10 MB limit.");
    }
}
