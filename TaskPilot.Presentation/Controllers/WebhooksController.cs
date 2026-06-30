using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using TaskPilot.Services.Interfaces.Payments;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    [AllowAnonymous]
    public class WebhooksController : ControllerBase
    {
        private readonly IWebhookService _webhookService;

        public WebhooksController(IWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        [HttpPost("{gateway}")]
        public async Task<IActionResult> Handle(string gateway)
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            
            var result = await _webhookService.HandleWebhookAsync(gateway, payload, Request.Headers);
            if (result.IsSuccess)
                return Ok();
                
            return BadRequest();
        }
    }
}
