using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Backlog;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common;

namespace TaskPilot.Presentation.Controllers
{
    [Authorize(Roles = "ProjectManager")]
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
            var lang = Request.Headers["lang"].ToString();
            if (string.IsNullOrEmpty(lang)) lang = "en";

            var result = await _backlogService.GetBacklogAsync(projectId, lang);
            return HandleResult(result);
        }

        [HttpPost("~/api/projects/{projectId:guid}/userstories")]
        public async Task<ActionResult> CreateUserStory(Guid projectId, [FromBody] CreateUserStoryDto request)
        {
            var result = await _backlogService.CreateUserStoryAsync(projectId, request);
            return HandleCreated(result, SuccessCodes.UserStory.Created);
        }

        [HttpPut("~/api/userstories/{storyId:guid}")]
        public async Task<ActionResult> UpdateUserStory(Guid storyId, [FromBody] UpdateUserStoryDto request)
        {
            var result = await _backlogService.UpdateUserStoryAsync(storyId, request);
            return HandleResult(result, SuccessCodes.UserStory.Updated);
        }

        [HttpDelete("~/api/userstories/{storyId:guid}")]
        public async Task<ActionResult> DeleteUserStory(Guid storyId)
        {
            var result = await _backlogService.DeleteUserStoryAsync(storyId);
            return HandleResult(result, SuccessCodes.UserStory.Deleted);
        }

        [HttpPost("~/api/userstories/{storyId:guid}/tasks")]
        public async Task<ActionResult> CreateTask(Guid storyId, [FromBody] CreateTaskDto request)
        {
            var result = await _backlogService.CreateTaskAsync(storyId, request);
            return HandleCreated(result, SuccessCodes.Task.Created);
        }

        [HttpPut("~/api/tasks/{taskId:guid}")]
        public async Task<ActionResult> UpdateTask(Guid taskId, [FromBody] UpdateTaskDto request)
        {
            var result = await _backlogService.UpdateTaskAsync(taskId, request);
            return HandleResult(result, SuccessCodes.Task.Updated);
        }

        [HttpDelete("~/api/tasks/{taskId:guid}")]
        public async Task<ActionResult> DeleteTask(Guid taskId)
        {
            var result = await _backlogService.DeleteTaskAsync(taskId);
            return HandleResult(result, SuccessCodes.Task.Deleted);
        }

        [HttpPost("~/api/projects/{projectId:guid}/backlog/regenerate")]
        public async Task<ActionResult> RegenerateBacklog(Guid projectId)
        {
            var result = await _regenerationService.RegenerateBacklogAsync(projectId);
            return HandleResult(result);
        }
    }
}
