using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Services.Assignment;
using TaskPilot.Models.Common.Results;
using Microsoft.AspNetCore.Authorization;
using TaskPilot.Models.Common;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Presentation.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/sprints/{sprintId:guid}/assignment")]
[Authorize(Roles = "Admin,ProjectManager")]
public class AssignmentController : ApiControllerBase
{
    private readonly IAssignmentScoringService _assignmentScoringService;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Project> _projectRepository;

    public AssignmentController(
        IAssignmentScoringService assignmentScoringService,
        IRepository<User> userRepository,
        IRepository<Project> projectRepository)
    {
        _assignmentScoringService = assignmentScoringService;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
    }

    [HttpGet("scores")]
    public async Task<ActionResult> GetScores(
        [FromRoute] Guid projectId,
        [FromRoute] Guid sprintId,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out Guid userId))
            return HandleResult(Result.Failure(CommonErrors.Unauthorized()));

        if (!User.IsInRole("Admin"))
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return HandleResult(Result.Failure(CommonErrors.Unauthorized()));

            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
                return HandleResult(Result.Failure(AssignmentErrors.InvalidProject));

            if (user.CompanyId != project.CompanyId)
                return HandleResult(Result.Failure(CommonErrors.Forbidden()));
        }

        var result = await _assignmentScoringService.ScoreAsync(projectId, sprintId, cancellationToken);
        
        // Sprint 8c is READ ONLY.
        // Therefore NO _unitOfWork.SaveChangesAsync(); must exist inside this controller.
        
        return HandleResult(result, SuccessCodes.Assignment.ScoringCompleted);
    }
}
