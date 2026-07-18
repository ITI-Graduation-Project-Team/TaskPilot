using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.AI;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/ai-projects")]
    [Authorize]
    public class AiProjectsController : ApiControllerBase
    {
        private readonly IAiProjectsService _aiProjectsService;

        public AiProjectsController(IAiProjectsService aiProjectsService)
        {
            _aiProjectsService = aiProjectsService;
        }

        [HttpPost("chat")]
        public async Task<IActionResult> SendMessage([FromBody] SendAiMessageDto request, CancellationToken cancellationToken)
        {
            var result = await _aiProjectsService.ProcessChatAsync(request, cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("upload-brd")]
        public async Task<IActionResult> UploadBrd(IFormFile file, [FromQuery] Guid? projectId, CancellationToken cancellationToken)
        {
            var result = await _aiProjectsService.UploadBrdAsync(file, projectId, cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateProject([FromBody] GenerateProjectDto request, CancellationToken cancellationToken)
        {
            var result = await _aiProjectsService.GenerateProjectAsync(request.ProjectId, request.ProjectName, cancellationToken);
            return HandleResult(result);
        }

        [HttpGet("{projectId}/chat")]
        public async Task<IActionResult> GetChatHistory(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _aiProjectsService.GetChatHistoryAsync(projectId, cancellationToken);
            return HandleResult(result);
        }

        [HttpPost("{projectId}/chat")]
        public async Task<IActionResult> SendFollowUpMessage(Guid projectId, [FromBody] SendAiMessageDto request, CancellationToken cancellationToken)
        {
            var result = await _aiProjectsService.ProcessFollowUpChatAsync(projectId, request.Message, cancellationToken);
            return HandleResult(result);
        }
    }
}
