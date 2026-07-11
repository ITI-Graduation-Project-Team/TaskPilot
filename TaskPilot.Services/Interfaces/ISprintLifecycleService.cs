namespace TaskPilot.Services.Interfaces
{
    public interface ISprintLifecycleService
    {
        /// <summary>
        /// Completes a sprint only when it is due. Returns false when the sprint
        /// is cancelled, deleted, missing, or its end date has been moved forward.
        /// </summary>
        Task<bool> EnsureCompletedIfDueAsync(Guid sprintId, CancellationToken cancellationToken = default);
    }
}
