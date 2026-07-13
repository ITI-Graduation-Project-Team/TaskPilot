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

        [HttpPost("paymob")]
        public async Task<IActionResult> HandlePaymob([FromQuery] string hmac)
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var headers = new HeaderDictionary();
            foreach (var header in Request.Headers)
                headers.Add(header.Key, header.Value);

            if (!string.IsNullOrEmpty(hmac))
                headers.Add("hmac", hmac);

            var result = await _webhookService.HandleWebhookAsync("paymob", payload, headers);
            if (result.IsSuccess)
                return Ok();

            if (result.Error.Description == "Invalid Signature")
            {
                _logger.LogWarning("Invalid webhook signature from Paymob");
                return BadRequest();
            }

            _logger.LogError("Webhook processing failed: {Error}", result.Error.Description);
            return Ok();
        }

        [HttpPost("paypal")]
        public async Task<IActionResult> HandlePayPal()
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
            
            var result = await _webhookService.HandleWebhookAsync("paypal", payload, Request.Headers);
            if (result.IsSuccess)
                return Ok();
                
            if (result.Error.Description == "Invalid Signature")
            {
                _logger.LogWarning("Invalid webhook signature from gateway PayPal");
                return BadRequest();
            }

            _logger.LogError("Webhook processing failed: {Error}", result.Error.Description);
            return Ok();
        }
    }
}
