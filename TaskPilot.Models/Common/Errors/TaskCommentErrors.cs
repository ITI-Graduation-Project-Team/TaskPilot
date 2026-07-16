using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Models.Common.Errors
{
    public static class TaskCommentErrors
    {
        public static readonly Error CommentNotFound = new(
            "TASK_COMMENT_NOT_FOUND",
            ErrorType.NotFound,
            "The requested task comment was not found.");

        public static readonly Error CommentForbidden = new(
            "TASK_COMMENT_FORBIDDEN",
            ErrorType.Forbidden,
            "You are not authorized to perform this action on the comment.");
    }
}
