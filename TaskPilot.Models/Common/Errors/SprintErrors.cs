namespace TaskPilot.Models.Common.Errors
{
    public static class SprintErrors
    {
        public static readonly Error NoUserStoriesSelected =
            new("NO_USER_STORIES_SELECTED", ErrorType.Validation, "At least one UserStory must be selected for the sprint.");
    }
}
