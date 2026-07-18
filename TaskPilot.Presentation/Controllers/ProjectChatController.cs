using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Orchestrators;
using TaskPilot.DTOs.Chat;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/chat")]
    [Authorize]
    public class ProjectChatController : ApiControllerBase
    {
        private readonly IProjectChatService _chatService;
        private readonly IProjectAiChatOrchestrator _aiChatOrchestrator;

        public ProjectChatController(
            IProjectChatService chatService,
            IProjectAiChatOrchestrator aiChatOrchestrator)
        {
            _chatService = chatService;
            _aiChatOrchestrator = aiChatOrchestrator;
        }

        [HttpGet]
        public async Task<IActionResult> GetSession(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _chatService.GetSessionAsync(projectId, cancellationToken);
            if (!result.IsSuccess)
            {
                // If the session is not found, maybe the user hasn't chatted yet. We could return an empty DTO.
                // But let's let the generic HandleResult process the NotFound.
                // However, an empty session DTO is better if it just doesn't exist yet, but it should exist after WBS gen.
            }
            return HandleResult(result);
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage(Guid projectId, [FromBody] SendChatMessageDto request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _aiChatOrchestrator.ProcessBacklogChatAsync(projectId, request.Message, cancellationToken);
                return HandleResult(Result<string>.Success(response));
            }
            catch (Exception ex)
            {
                return HandleResult(Result<string>.Failure(new Error("ServerError", ErrorType.Failure, ex.Message)));
            }
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmUpdates(Guid projectId, CancellationToken cancellationToken)
        {
            try
            {
                var summary = await _aiChatOrchestrator.ConfirmBacklogUpdatesAsync(projectId, cancellationToken);
                return HandleResult(Result<string>.Success(summary));
            }
            catch (Exception ex)
            {
                return HandleResult(Result<string>.Failure(new Error("ServerError", ErrorType.Failure, ex.Message)));
            }
        }
    }
}
