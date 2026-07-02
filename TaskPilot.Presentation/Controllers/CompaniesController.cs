using Microsoft.AspNetCore.Authorization;
using TaskPilot.Models.Common;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Company;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Presentation.Controllers
{
    //[Authorize]
    [Route("api/companies")]
    public class CompaniesController
        : ApiControllerBase
    {
        private readonly ICompanyService _companyService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRepository<User> _userRepository;

        public CompaniesController(
            ICompanyService companyService,
            IUnitOfWork unitOfWork,
            IRepository<User> userRepository)
        {
            _companyService = companyService;
            _unitOfWork = unitOfWork;
            _userRepository = userRepository;
        }

        [HttpPost("setup")]
        public async Task<ActionResult>
            SetupCompany(
                [FromForm]
                SetupCompanyRequest request)
            {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(
                    userId,
                    out Guid ownerId))
            {
                return HandleResult(Result.Failure<CompanyResponse>(CommonErrors.Unauthorized()));
            }

            var result =
                await _companyService
                    .SetupCompanyAsync(
                        request,
                        ownerId);

            if (result.IsSuccess)
            {
                await _unitOfWork
                    .SaveChangesAsync();
            }

            return HandleCreated(result, SuccessCodes.Company.Setup);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpPost("employees/invite")]
        public async Task<ActionResult> InviteEmployees(
            [FromBody] InviteEmployeesRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
            {
                return Unauthorized();
            }

            var result = await _companyService.InviteEmployeesAsync(request, ownerId);

            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            return HandleResult(result, SuccessCodes.Company.EmployeeInvitationsSent);
        }
        [Authorize(Roles = "ProjectManager")]
        [HttpGet("employees/search")]
        public async Task<ActionResult> SearchEmployees(
           [FromQuery] string query)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
                return Unauthorized();

            var result = await _companyService.SearchEmployeesAsync(query, ownerId);

            return HandleResult(result, SuccessCodes.Company.EmployeesSearched);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpGet("invitations")]
        public async Task<ActionResult> GetInvitations([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
                return Unauthorized();

            if (page < 1)
                return HandleResult(Result.Failure<PagedResult<CompanyInvitationDto>>(CompanyErrors.InvalidPageNumber));

            if (pageSize < 1 || pageSize > 100)
                return HandleResult(Result.Failure<PagedResult<CompanyInvitationDto>>(CompanyErrors.InvalidPageSize));

            TaskPilot.Models.Enums.InvitationStatus parsedStatus = TaskPilot.Models.Enums.InvitationStatus.All;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (!Enum.TryParse(status, true, out parsedStatus) || !Enum.IsDefined(typeof(TaskPilot.Models.Enums.InvitationStatus), parsedStatus))
                {
                    return HandleResult(Result.Failure<PagedResult<CompanyInvitationDto>>(CompanyErrors.InvalidInvitationStatus));
                }
            }

            var result = await _companyService.GetInvitationsAsync(ownerId, parsedStatus, page, pageSize);
            return HandleResult(result, "INVITATIONS_RETRIEVED_SUCCESS");
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpDelete("invitations/{invitationId}")]
        public async Task<ActionResult> CancelInvitation([FromRoute] Guid invitationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
                return Unauthorized();

            var result = await _companyService.CancelInvitationAsync(invitationId, ownerId);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync();
            }
            return HandleResult(result, "INVITATION_CANCELLED_SUCCESS");
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpPost("invitations/{invitationId}/resend")]
        public async Task<ActionResult> ResendInvitation([FromRoute] Guid invitationId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
                return Unauthorized();

            var result = await _companyService.ResendInvitationAsync(invitationId, ownerId);
            return HandleResult(result, "INVITATION_RESENT_SUCCESS");
        }

        [HttpGet("{companyId}/employees")]
        public async Task<ActionResult> GetCompanyEmployees(
    [FromRoute] Guid companyId,
    CancellationToken cancellationToken)
        {
            // 1. Get current user id from claims
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            // 2. Load current user
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return Unauthorized();

            // 3. Admin can access any company
            if (User.IsInRole("Admin"))
            {
                var adminResult = await _companyService
                    .GetCompanyEmployeesAsync(companyId, cancellationToken);
                return HandleResult(adminResult);
            }

            // 4. ProjectManager must belong to the requested company
            if (!User.IsInRole("ProjectManager"))
                return StatusCode(403,
                    "Only Project Managers can view company employees.");

            if (user.CompanyId != companyId)
                return StatusCode(403,
                    "You do not have access to this company's employees.");

            // 5. Same company ProjectManager — allow access
            var result = await _companyService
                .GetCompanyEmployeesAsync(companyId, cancellationToken);
            return HandleResult(result);
        }
    }
}