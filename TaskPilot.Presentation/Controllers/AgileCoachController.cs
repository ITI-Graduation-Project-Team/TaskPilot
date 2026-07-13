using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.AgileCoach;
using TaskPilot.Models.Common.Results;
using TaskPilot.Services.Interfaces;
using TaskPilot.AI.Constants; // if needed
using TaskPilot.Models.Common;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/agile-coach")]
    [Authorize]
    public class AgileCoachController : ApiControllerBase
    {
        private readonly IAgileCoachService _agileCoachService;
        private readonly IUnitOfWork _unitOfWork;

        public AgileCoachController(IAgileCoachService agileCoachService, IUnitOfWork unitOfWork)
        {
            _agileCoachService = agileCoachService;
            _unitOfWork = unitOfWork;
        }

        private string GetLang()
        {
            var lang = Request.Headers["lang"].ToString();
            return string.IsNullOrEmpty(lang) ? "en" : lang.ToLower();
        }

        [HttpGet("summary/{taskId:guid}")]
        public async Task<IActionResult> GetSummary(Guid taskId)
        {
            var lang = GetLang();

            var result = await _agileCoachService.GetOrGenerateSummaryAsync(taskId, lang);

            if (!result.IsSuccess)
                return HandleResult(result, null);

            if (result.Value!.Summary.IsNewlyGenerated)
                await _unitOfWork.SaveChangesAsync();

            var successCode = result.Value.Summary.IsNewlyGenerated
                ? SuccessCodes.AgileCoach.SummaryGenerated
                : SuccessCodes.AgileCoach.SummaryRetrieved;

            return HandleResult(Result.Success(result.Value.Summary), successCode);
        }

        [HttpPost("summary/{taskId:guid}/regenerate")]
        public async Task<IActionResult> RegenerateSummary(Guid taskId)
        {
            var lang = GetLang();

            var result = await _agileCoachService.RegenerateSummaryAsync(taskId, lang);

            if (!result.IsSuccess)
                return HandleResult(result, null);

            await _unitOfWork.SaveChangesAsync();

            return HandleResult(
                Result.Success(result.Value!.Summary),
                SuccessCodes.AgileCoach.SummaryGenerated);
        }

        [HttpPost("chat/stream")]
        [DisableRequestSizeLimit]
        public async Task StreamChat([FromBody] AgileCoachChatRequest request)
        {
            var lang = GetLang();

            var bufferingFeature = HttpContext.Features.Get<IHttpResponseBodyFeature>();
            bufferingFeature?.DisableBuffering();

            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            await Response.Body.FlushAsync();

            var stream = _agileCoachService.StreamChatAsync(
                request.TaskItemId,
                request.Message,
                request.History,
                lang);

            await foreach (var chunk in stream)
            {
                if (chunk.StartsWith("__ERROR__:"))
                {
                    // Terminal error event — controller detects the sentinel
                    // written by the service and converts it to an SSE error event
                    var errorCode = chunk["__ERROR__:".Length..];
                    var errorEvent = $"event: error\ndata: {errorCode}\n\n";
                    await Response.WriteAsync(errorEvent);
                    await Response.Body.FlushAsync();
                    break;
                }

                var sseData = $"data: {chunk}\n\n";
                await Response.WriteAsync(sseData);
                await Response.Body.FlushAsync();
            }

            // Send terminal done event so the client knows the stream ended
            await Response.WriteAsync("event: done\ndata: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
    }
}
