using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common;

namespace TaskPilot.Presentation.Controllers
{
    //[Authorize]
    [Route("api/[controller]")]
    [ApiController]

    public class BacklogController : ApiControllerBase
    {
        private readonly IBacklogService _backlogService;

        private readonly IBacklogRegenerationService _regenerationService;

        public BacklogController(IBacklogService backlogService, IBacklogRegenerationService regenerationService)
        {
            _backlogService = backlogService;
            _regenerationService = regenerationService;
        }

        [HttpGet("~/api/projects/{projectId:guid}/backlog")]
        public async Task<ActionResult> GetBacklog(Guid projectId)
        {
            var result = await _backlogService.GetBacklogAsync(projectId);
            return HandleResult(result);
        }

        //[Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost("~/api/projects/{projectId:guid}/userstories")]
        public async Task<ActionResult> CreateUserStory(Guid projectId, [FromBody] CreateUserStoryDto request)
        {
            var result = await _backlogService.CreateUserStoryAsync(projectId, request);
            return HandleCreated(result, SuccessCodes.Backlog.UserStoryCreated);
        }

        //[Authorize(Roles = "Admin,ProjectManager")]
        [HttpPut("~/api/userstories/{storyId:guid}")]
        public async Task<ActionResult> UpdateUserStory(Guid storyId, [FromBody] UpdateUserStoryDto request)
        {
            var result = await _backlogService.UpdateUserStoryAsync(storyId, request);
            return HandleResult(result, SuccessCodes.Backlog.UserStoryUpdated);
        }

        //[Authorize(Roles = "Admin,ProjectManager")]
        [HttpDelete("~/api/userstories/{storyId:guid}")]
        public async Task<ActionResult> DeleteUserStory(Guid storyId)
        {
            var result = await _backlogService.DeleteUserStoryAsync(storyId);
            return HandleResult(result, SuccessCodes.Backlog.UserStoryDeleted);
        }

        //[Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost("~/api/userstories/{storyId:guid}/tasks")]
        public async Task<ActionResult> CreateTask(Guid storyId, [FromBody] CreateTaskDto request)
        {
            var result = await _backlogService.CreateTaskAsync(storyId, request);
            return HandleCreated(result, SuccessCodes.Backlog.TaskCreated);
        }

        //[Authorize(Roles = "Admin,ProjectManager")]
        [HttpPut("~/api/tasks/{taskId:guid}")]
        public async Task<ActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskDto request)
        {
            var result = await _backlogService.UpdateTaskAsync(taskId, request);
            return HandleResult(result, SuccessCodes.Backlog.TaskUpdated);
        }

        //[Authorize(Roles = "Admin,ProjectManager")]
        [HttpDelete("~/api/tasks/{taskId:guid}")]
        public async Task<ActionResult> DeleteTask(Guid taskId)
        {
            var result = await _backlogService.DeleteTaskAsync(taskId);
            return HandleResult(result, SuccessCodes.Backlog.TaskDeleted);
        }

        //[Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost("~/api/projects/{projectId:guid}/backlog/regenerate")]
        public async Task<ActionResult> RegenerateBacklog(Guid projectId)
        {
            var result = await _regenerationService.RegenerateBacklogAsync(projectId);
            return HandleResult(result);
        }
    }
}
