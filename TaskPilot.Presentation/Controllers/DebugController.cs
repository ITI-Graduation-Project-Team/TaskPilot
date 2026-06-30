using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Qdrant.Client;
using Microsoft.Extensions.Options;
using TaskPilot.AI.Options;
using System;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/debug")]
    public class DebugController : ControllerBase
    {
        private readonly QdrantClient _client;
        private readonly string _collectionName;
        private readonly QdrantOptions _qdrantOptions;
        private readonly string _initUrl;

        public DebugController(IOptions<QdrantOptions> options)
        {
            _qdrantOptions = options.Value;
            _collectionName = string.IsNullOrWhiteSpace(_qdrantOptions.CollectionName) ? "taskpilot_knowledge" : _qdrantOptions.CollectionName;
            
            var url = _qdrantOptions.Url;
            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            
            _initUrl = url;

            if (!string.IsNullOrEmpty(url))
            {
                var uri = new Uri(url);
                var port = uri.IsDefaultPort ? 6334 : uri.Port; // Default to Qdrant gRPC port if unspecified
                var https = uri.Scheme == "https";

                _client = new QdrantClient(host: uri.Host, port: port, https: https, apiKey: _qdrantOptions.ApiKey);
            }
            else
            {
                _client = new QdrantClient("localhost", 6334);
            }
        }

        [HttpGet("qdrant")]
        public async Task<IActionResult> CheckQdrant()
        {
            try
            {
                var exists = await _client.CollectionExistsAsync(_collectionName);
                return Ok(new
                {
                    connected = true,
                    collectionExists = exists,
                    collectionName = _collectionName
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    connected = false,
                    collectionExists = false,
                    collectionName = _collectionName,
                    error = ex.Message
                });
            }
        }

        [HttpGet("qdrant/details")]
        public async Task<IActionResult> CheckQdrantDetails()
        {
            bool authenticated = false;
            bool collectionExists = false;

            try
            {
                collectionExists = await _client.CollectionExistsAsync(_collectionName);
                authenticated = true; // If we can check collection existence without error, we are connected and authenticated
            }
            catch (Exception)
            {
                // We leave authenticated and collectionExists as false
            }

            return Ok(new
            {
                url = _initUrl,
                connectionMode = "gRPC",
                authenticated = authenticated,
                collectionExists = collectionExists,
                sdk = "Qdrant.Client"
            });
        }

        [HttpGet("projects")]
        public async Task<IActionResult> GetProjects([FromServices] ApplicationDbContext dbContext)
        {
            var projects = await dbContext.Projects.Select(p => new { p.Id }).ToListAsync();
            return Ok(projects);
        }

        [HttpGet("test-wbs/mock")]
        public async Task<IActionResult> TestWbsGenerationMock(
            [FromServices] WBSGenerationAgent wbsAgent)
        {
            var snapshot = new TaskPilot.Models.Entities.RequirementsSnapshot
            {
                BusinessRequirements = new System.Collections.Generic.List<string> { "The system must allow users to log in securely." },
                TechnicalRequirements = new System.Collections.Generic.List<string> { "Use EF Core and Identity.", "Use SQL Server." },
                Constraints = new System.Collections.Generic.List<string> { "Must be completed in 1 month." },
                Integrations = new System.Collections.Generic.List<string> { "Sendgrid for email." },
                ScaleRequirements = new System.Collections.Generic.List<string> { "Handle 100 concurrent users." }
            };

            try
            {
                var wbs = await wbsAgent.GenerateAsync(
                    snapshot,
                    new System.Collections.Generic.List<string>(),
                    new System.Collections.Generic.List<string>(),
                    string.Empty);
                return Ok(wbs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
            }
        }
    }
}
