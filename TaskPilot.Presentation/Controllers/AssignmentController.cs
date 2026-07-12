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
using TaskPilot.DTOs.Assignment;
using TaskPilot.Data.Repositories.Interfaces;

namespace TaskPilot.Presentation.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/sprints/{sprintId:guid}/assignment")]
[Authorize(Roles = "Admin,ProjectManager")]
public class AssignmentController : ApiControllerBase
{
    private readonly IAssignmentExplanationService _assignmentExplanationService;
    private readonly IAssignmentConfirmationService _confirmationService;
    private readonly ITaskRepository _taskRepository;
    private readonly IRepository<TaskItem> _baseTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Project> _projectRepository;

    public AssignmentController(
        IAssignmentExplanationService assignmentExplanationService,
        IAssignmentConfirmationService confirmationService,
        ITaskRepository taskRepository,
        IRepository<TaskItem> baseTaskRepository,
        IUnitOfWork unitOfWork,
        IRepository<User> userRepository,
        IRepository<Project> projectRepository)
    {
        _assignmentExplanationService = assignmentExplanationService;
        _confirmationService = confirmationService;
        _taskRepository = taskRepository;
        _baseTaskRepository = baseTaskRepository;
        _unitOfWork = unitOfWork;
        _userRepository = userRepository;
        _projectRepository = projectRepository;
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult> GetSuggestions(
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

        var result = await _assignmentExplanationService.GenerateAsync(projectId, sprintId, cancellationToken);
        
        // Sprint 8c is READ ONLY.
        // Therefore NO _unitOfWork.SaveChangesAsync(); must exist inside this controller.
        
        return HandleResult(result, SuccessCodes.Assignment.ExplanationsGenerated);
    }

    /// <summary>
    /// Confirm PM-reviewed assignments in bulk.
    /// Partial confirm allowed — tasks not included remain unchanged.
    /// Override allowed — tasks already assigned will be reassigned.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(
        Guid projectId,
        Guid sprintId,
        [FromBody] ConfirmAssignmentsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _confirmationService
            .ConfirmAsync(projectId, sprintId, request, cancellationToken);

        if (result.IsSuccess && result.Value!.AssignmentsConfirmed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return HandleResult(result, SuccessCodes.Assignment.AssignmentsConfirmed);
    }

    /// <summary>
    /// Remove assignment from a single task (reset to unassigned).
    /// </summary>
    [HttpDelete("tasks/{taskId}")]
    public async Task<IActionResult> UnassignTask(
        Guid projectId,
        Guid sprintId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await _baseTaskRepository.GetByIdAsync(taskId);

        if (task is null || task.SprintId != sprintId)
            return HandleResult(Result.Failure(CommonErrors.NotFound()));

        task.EmployeeId = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return HandleResult(Result.Success(), SuccessCodes.Assignment.TaskUnassigned);
    }
}
