using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Assignment;

namespace TaskPilot.Presentation.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/sprints/{sprintId:guid}/assignment")]
[Authorize(Roles = "Admin,ProjectManager")]
public class AssignmentController : ApiControllerBase
{
    private readonly IAssignmentScoringService _scoringService;
    private readonly IAssignmentConfirmationService _confirmationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<Sprint> _sprintRepository;
    private readonly IRepository<ProjectEmployee> _projectEmployeeRepository;

    public AssignmentController(
        IAssignmentScoringService scoringService,
        IAssignmentConfirmationService confirmationService,
        IUnitOfWork unitOfWork,
        IRepository<Project> projectRepository,
        IRepository<Sprint> sprintRepository,
        IRepository<ProjectEmployee> projectEmployeeRepository)
    {
        _scoringService = scoringService;
        _confirmationService = confirmationService;
        _unitOfWork = unitOfWork;
        _projectRepository = projectRepository;
        _sprintRepository = sprintRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult> GetSuggestions(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var access = await ValidateProjectAccessAsync(projectId);
        if (access.IsFailure)
            return HandleResult(access);

        var result = await _scoringService.ScoreAsync(projectId, sprintId, cancellationToken);
        return HandleResult(result);
    }

    [HttpGet("team")]
    public async Task<ActionResult> GetAssignmentTeam(
        Guid projectId,
        Guid sprintId,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAccessAndSprintAsync(projectId, sprintId);
        if (validation.IsFailure)
            return HandleResult(validation);

        var team = await _projectEmployeeRepository.GetQueryable()
            .AsNoTracking()
            .Where(pe => pe.ProjectId == projectId && pe.IsActive && !pe.Employee.IsDeactivated)
            .OrderBy(pe => pe.Employee.FirstNameEn)
            .ThenBy(pe => pe.Employee.LastNameEn)
            .Select(pe => new AssignmentTeamMemberDto
            {
                EmployeeId = pe.EmployeeId,
                FullName = (pe.Employee.FirstNameEn + " " + pe.Employee.LastNameEn).Trim(),
                JobTitle = pe.Employee.JobTitle ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return HandleResult(Result.Success(team));
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        Guid projectId,
        Guid sprintId,
        [FromBody] ConfirmAssignmentsRequest request,
        CancellationToken cancellationToken)
    {
        var access = await ValidateProjectAccessAsync(projectId);
        if (access.IsFailure)
            return HandleResult(access);

        var result = await _confirmationService.ConfirmAsync(projectId, sprintId, request, cancellationToken);
        if (result.IsSuccess && result.Value!.AssignmentsConfirmed > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return HandleResult(result, SuccessCodes.Assignment.AssignmentsConfirmed);
    }

    [HttpPatch("tasks/{taskId:guid}")]
    public async Task<IActionResult> AssignTask(
        Guid projectId,
        Guid sprintId,
        Guid taskId,
        [FromBody] AssignTaskRequest request,
        CancellationToken cancellationToken)
    {
        var access = await ValidateProjectAccessAsync(projectId);
        if (access.IsFailure)
            return HandleResult(access);

        var result = await _confirmationService.AssignTaskAsync(projectId, sprintId, taskId, request, cancellationToken);
        if (result.IsSuccess && result.Value!.Changed)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return HandleResult(result);
    }

    [HttpDelete("tasks/{taskId:guid}")]
    public Task<IActionResult> UnassignTask(
        Guid projectId,
        Guid sprintId,
        Guid taskId,
        CancellationToken cancellationToken)
        => AssignTask(projectId, sprintId, taskId, new AssignTaskRequest(), cancellationToken);

    private async Task<Result> ValidateAccessAndSprintAsync(Guid projectId, Guid sprintId)
    {
        var access = await ValidateProjectAccessAsync(projectId);
        if (access.IsFailure)
            return access;

        var sprint = await _sprintRepository.GetByIdAsync(sprintId);
        if (sprint == null)
            return Result.Failure(AssignmentErrors.SprintNotFound);
        if (sprint.ProjectId != projectId)
            return Result.Failure(AssignmentErrors.SprintDoesNotBelongToProject);
        if (sprint.Status != SprintStatus.Planned)
            return Result.Failure(AssignmentErrors.SprintNotPlanned);

        return Result.Success();
    }

    private async Task<Result> ValidateProjectAccessAsync(Guid projectId)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return Result.Failure(CommonErrors.Unauthorized());

        if (User.IsInRole("Admin"))
            return Result.Success();

        var canManageProject = await _projectRepository.AnyAsync(
            project => project.Id == projectId && project.ManagerId == userId);
        if (!canManageProject)
            return Result.Failure(CommonErrors.Forbidden());

        return Result.Success();
    }
}
