namespace TaskPilot.DTOs.Sprints
{
    public enum ReviewTaskAction
    {
        AcceptAll,    // Mark all Review tasks as Done
        SendToBacklog // Move all Review tasks back to ToDo
    }

    public class CompleteSprintRequest
    {
        // If null, the sprint will only be completed if there are no Review tasks.
        // If provided, the backend applies this action to Review tasks before completing.
        public ReviewTaskAction? ReviewAction { get; set; }
    }
}
