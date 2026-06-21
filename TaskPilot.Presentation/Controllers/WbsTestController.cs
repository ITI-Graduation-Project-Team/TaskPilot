using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;

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

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> Generate(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project =
            await _projectRepository.GetByIdAsync(projectId);

        if (project is null)
            return NotFound("Project not found.");

        if (project.RequirementsSnapshot is null)
            return BadRequest("Project has no RequirementsSnapshot.");

        var result =
            await _wbsAgent.GenerateAsync(
                project.RequirementsSnapshot,
                cancellationToken);

        return Ok(result);
    }
}