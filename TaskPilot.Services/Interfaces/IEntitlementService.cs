using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IEntitlementService
    {
        Task<Result> EnsureCanCreateProjectAsync(Guid managerId, CancellationToken cancellationToken = default);
        Task<Result> EnsureCanAddTeamMembersAsync(Guid projectId, int countToAdd, CancellationToken cancellationToken = default);
        Task<Result> EnsureCanUploadAsync(Guid ownerPmId, long incomingFileSizeBytes, long existingFileSizeBytesBeingReplaced = 0, CancellationToken cancellationToken = default);
        Task UpdateStorageUsageAsync(Guid ownerPmId, long netBytesChange, CancellationToken cancellationToken = default);
    }
}
