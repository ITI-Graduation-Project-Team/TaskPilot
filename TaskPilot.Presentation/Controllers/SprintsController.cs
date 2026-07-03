using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Assignment;
using TaskPilot.Models.Common.Results;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Common.Errors;
using TaskPilot.DTOs.Assignment;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/sprints")]
    public class SprintsController : ApiControllerBase
    {
        private readonly ISprintConfirmationService _sprintConfirmationService;
        private readonly ITeamSnapshotService _teamSnapshotService;
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Project> _projectRepository;

        public SprintsController(
            ISprintConfirmationService sprintConfirmationService,
            ITeamSnapshotService teamSnapshotService,
            IRepository<User> userRepository,
            IRepository<Project> projectRepository)
        {
            _sprintConfirmationService = sprintConfirmationService;
            _teamSnapshotService = teamSnapshotService;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
        }

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

        [Authorize(Roles = "Admin,ProjectManager")]
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
    }
}
