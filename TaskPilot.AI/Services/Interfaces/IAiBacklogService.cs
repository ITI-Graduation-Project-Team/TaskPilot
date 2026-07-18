using System;
using System.Threading.Tasks;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IAiBacklogService
    {
        Task<Result<BacklogDto>> GetBacklogAsync(Guid projectId);
        Task<Result<UserStoryDto>> CreateUserStoryAsync(Guid projectId, CreateUserStoryDto request);
        Task<Result> UpdateUserStoryAsync(Guid storyId, UpdateUserStoryDto request);
        Task<Result> DeleteUserStoryAsync(Guid storyId);
    }
}
