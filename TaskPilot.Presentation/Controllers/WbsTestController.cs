using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/test/wbs")]
    public class WbsTestController : ControllerBase
    {
        private readonly WBSGenerationAgent _wbsAgent;
        private readonly IRepository<Project> _projectRepository;

        public WbsTestController(
            WBSGenerationAgent wbsAgent,
            IRepository<Project> projectRepository)
        {
            _wbsAgent = wbsAgent;
            _projectRepository = projectRepository;
        }

        /// <summary>
        /// TEMPORARY — for Sprint 5b verification only.
        /// Remove before production.
        /// </summary>
        [HttpGet("{projectId}")]
        public async Task<IActionResult> GenerateWbs(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var project = await _projectRepository
                .GetByIdAsync(projectId, p => p.RequirementsSnapshot);

            if (project is null)
                return NotFound("Project not found.");

            if (project.RequirementsSnapshot is null)
                return BadRequest(
                    "Project has no RequirementsSnapshot. " +
                    "Complete requirement finalization first.");

            var result = await _wbsAgent.GenerateAsync(
                project.RequirementsSnapshot,
                cancellationToken);

            return Ok(result);
        }
    }
}