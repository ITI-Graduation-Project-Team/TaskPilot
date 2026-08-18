using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Assignment;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Assignment;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/sprints")]
    public class SprintsController : ApiControllerBase
    {
        private readonly ISprintConfirmationService _sprintConfirmationService;
        private readonly ITeamSnapshotService _teamSnapshotService;
        private readonly ICapacityValidationService _capacityValidationService;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Sprint> _sprintRepository;
        private readonly ISprintLifecycleService _sprintLifecycleService;
        private readonly IUnitOfWork _unitOfWork;

        public SprintsController(
            ISprintConfirmationService sprintConfirmationService,
            ITeamSnapshotService teamSnapshotService,
            ICapacityValidationService capacityValidationService,
            IRepository<User> userRepository,
            IRepository<Project> projectRepository,
            IRepository<Sprint> sprintRepository,
            ISprintLifecycleService sprintLifecycleService,
            IUnitOfWork unitOfWork)
        {
            _sprintConfirmationService = sprintConfirmationService;
            _teamSnapshotService = teamSnapshotService;
            _capacityValidationService = capacityValidationService;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
            _sprintRepository = sprintRepository;
            _sprintLifecycleService = sprintLifecycleService;
            _unitOfWork = unitOfWork;
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpPost("confirm")]
        public async Task<ActionResult> Confirm(
            Guid projectId,
            [FromBody] ConfirmSprintRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _sprintConfirmationService
                .ConfirmAsync(projectId, request, cancellationToken);

            return HandleResult(result);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpGet("{sprintId:guid}/assignment/snapshot")]
        public async Task<ActionResult> GetSnapshot(
            Guid projectId,
            Guid sprintId,
            CancellationToken cancellationToken)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId))
                return HandleResult(Result.Failure<SprintAssignmentSnapshotDto>(CommonErrors.Unauthorized()));

            if (!User.IsInRole("Admin"))
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return HandleResult(Result.Failure<SprintAssignmentSnapshotDto>(CommonErrors.Unauthorized()));

                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                    return HandleResult(Result.Failure<SprintAssignmentSnapshotDto>(AssignmentErrors.ProjectNotFound));

                if (user.CompanyId != project.CompanyId)
                    return HandleResult(Result.Failure<SprintAssignmentSnapshotDto>(CommonErrors.Forbidden()));
            }

            var result = await _teamSnapshotService.GetSnapshotAsync(projectId, sprintId, cancellationToken);
            return HandleResult(result);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpGet("{sprintId:guid}/assignment/validate")]
        public async Task<ActionResult> ValidateAssignment(
            Guid projectId,
            Guid sprintId,
            CancellationToken cancellationToken)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId))
                return HandleResult(Result.Failure<CapacityValidationResult>(CommonErrors.Unauthorized()));

            if (!User.IsInRole("Admin"))
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    return HandleResult(Result.Failure<CapacityValidationResult>(CommonErrors.Unauthorized()));

                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                    return HandleResult(Result.Failure<CapacityValidationResult>(AssignmentErrors.ProjectNotFound));

                if (user.CompanyId != project.CompanyId)
                    return HandleResult(Result.Failure<CapacityValidationResult>(CommonErrors.Forbidden()));
            }

            var result = await _capacityValidationService.ValidateAsync(projectId, sprintId, cancellationToken);
            return HandleResult(result);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpPost("{sprintId:guid}/start")]
        public async Task<ActionResult> StartSprint(Guid projectId, Guid sprintId, CancellationToken cancellationToken)
        {
            var result = await _sprintLifecycleService.StartSprintAsync(projectId, sprintId, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result, SuccessCodes.Sprint.Started);
        }
        [Authorize(Roles = "Admin,ProjectManager")]
        [HttpPost("{sprintId:guid}/cancel")]
        public async Task<ActionResult> CancelSprint(Guid projectId, Guid sprintId, CancellationToken cancellationToken)
        {
            var result = await _sprintLifecycleService.CancelSprintAsync(projectId, sprintId, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result, SuccessCodes.Sprint.Cancelled);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpPost("{sprintId:guid}/complete")]
        public async Task<ActionResult> CompleteSprint(Guid projectId, Guid sprintId, [FromBody] CompleteSprintRequest? request, CancellationToken cancellationToken)
        {
            var result = await _sprintLifecycleService.CompleteSprintAsync(projectId, sprintId, request?.ReviewAction, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result, SuccessCodes.Sprint.Completed);
        }

        [Authorize(Roles = "ProjectManager,Employee")]
        [HttpGet("active")]
        public async Task<ActionResult> GetActiveSprint(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _sprintLifecycleService.GetActiveSprintAsync(projectId, cancellationToken);
            return HandleResult(result, SuccessCodes.Sprint.ActiveRetrieved);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpGet("planned")]
        public async Task<ActionResult> GetPlannedSprint(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _sprintLifecycleService.GetPlannedSprintAsync(projectId, cancellationToken);
            return HandleResult(result, SuccessCodes.Sprint.ActiveRetrieved);
        }

        [HttpGet]
        [Authorize(Roles = "ProjectManager")]
        public async Task<IActionResult> GetAllSprints(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? statusFilter = null, [FromQuery] string? dateFrom = null, [FromQuery] string? dateTo = null, CancellationToken cancellationToken = default)
        {
            if (page < 1) return HandleResult(Result.Failure<PagedResult<SprintListItemDto>>(SprintErrors.InvalidPageNumber));
            if (pageSize < 1 || pageSize > 100) return HandleResult(Result.Failure<PagedResult<SprintListItemDto>>(SprintErrors.InvalidPageSize));

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId)) 
                return HandleResult(Result.Failure<PagedResult<SprintListItemDto>>(CommonErrors.Unauthorized()));

            var lang = Request.Headers["lang"].ToString();
            if (string.IsNullOrEmpty(lang)) lang = "en";

            var result = await _sprintLifecycleService.GetAllSprintsPagedAsync(projectId, userId, page, pageSize, statusFilter, dateFrom, dateTo, lang, cancellationToken);
            return HandleResult(result);
        }

        [HttpGet("completed/latest")]
        [Authorize(Roles = "ProjectManager")]
        public async Task<IActionResult> GetLatestCompletedSprint(Guid projectId)
        {
            var result = await _sprintLifecycleService.GetLatestCompletedSprintAsync(projectId);
            return HandleResult(result);
        }

        [Authorize(Roles = "ProjectManager,Employee")]
        [HttpGet("{sprintId:guid}/tasks")]
        public async Task<ActionResult> GetSprintTasks(
            Guid projectId,
            Guid sprintId,
            CancellationToken cancellationToken)
        {
            var result = await _sprintLifecycleService.GetSprintTasksAsync(projectId, sprintId, cancellationToken);
            return HandleResult(result);
        }
    }
}
