using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using TaskPilot.Services.Interfaces.Payments;
using Microsoft.Extensions.Logging;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    [AllowAnonymous]
    public class WebhooksController : ControllerBase
    {
        private readonly IWebhookService _webhookService;
        private readonly ILogger<WebhooksController> _logger;

        public WebhooksController(IWebhookService webhookService, ILogger<WebhooksController> logger)
        {
            _webhookService = webhookService;
            _logger = logger;
        }

        [HttpPost("{gateway}")]
        public async Task<IActionResult> Handle(string gateway)
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
            
            var result = await _webhookService.HandleWebhookAsync(gateway, payload, Request.Headers);
            if (result.IsSuccess)
                return Ok();
                
            if (result.Error.Description == "Invalid Signature")
            {
                _logger.LogWarning("Invalid webhook signature from gateway {Gateway}", gateway);
                return BadRequest();
            }

            _logger.LogError("Webhook processing failed: {Error}", result.Error.Description);
            return Ok();
        }
    }
}
