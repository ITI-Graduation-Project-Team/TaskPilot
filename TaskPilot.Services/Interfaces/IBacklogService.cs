using System;
using System.Threading.Tasks;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IBacklogService
    {
        Task<Result<BacklogDto>> GetBacklogAsync(Guid projectId);
        Task<Result<UserStoryDto>> CreateUserStoryAsync(Guid projectId, CreateUserStoryDto request);
        Task<Result> UpdateUserStoryAsync(Guid storyId, UpdateUserStoryDto request);
        Task<Result> DeleteUserStoryAsync(Guid storyId);
        Task<Result<TaskItemDto>> CreateTaskAsync(Guid storyId, CreateTaskDto request);
        Task<Result> UpdateTaskAsync(Guid taskId, UpdateTaskDto request);
        Task<Result> DeleteTaskAsync(Guid taskId);
    }
}
