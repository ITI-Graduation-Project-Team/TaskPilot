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

        [Authorize(Roles = "ProjectManager")]
        [HttpGet("employees")]
        public async Task<ActionResult> GetCompanyEmployees(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool? isDeactivated = null,
            CancellationToken cancellationToken = default)
        {
            // 1. Get current user id from claims
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            // 2. Load current user
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return Unauthorized();

            // 3. Ensure user belongs to a company
            if (!user.CompanyId.HasValue)
                return StatusCode(403, "You do not belong to a company.");

            if (page < 1)
                return HandleResult(Result.Failure<PagedResult<CompanyEmployeeDto>>(CompanyErrors.InvalidPageNumber));

            if (pageSize < 1 || pageSize > 100)
                return HandleResult(Result.Failure<PagedResult<CompanyEmployeeDto>>(CompanyErrors.InvalidPageSize));

            // 4. Fetch employees
            var result = await _companyService
                .GetCompanyEmployeesAsync(user.CompanyId.Value, page, pageSize, isDeactivated, cancellationToken);
            return HandleResult(result);
        }
        [Authorize(Roles = "ProjectManager")]
        [HttpGet("employees/{employeeId}")]
        public async Task<ActionResult> GetCompanyEmployee(
            [FromRoute] string employeeId,
            CancellationToken cancellationToken = default)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId))
                return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return Unauthorized();

            if (!user.CompanyId.HasValue)
                return StatusCode(403, "You do not belong to a company.");

            var result = await _companyService
                .GetCompanyEmployeeByIdAsync(user.CompanyId.Value, employeeId, cancellationToken);
            return HandleResult(result);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpPut("{companyId}")]
        public async Task<IActionResult> UpdateCompany(
            [FromRoute] Guid companyId,
            [FromForm] UpdateCompanyDto request)
        {
            // Extract the owner ID from the authenticated user's token claims
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
            {
                return Unauthorized(CommonErrors.Unauthorized("User is not authenticated."));
            }

            // Pass the data to the service layer for business logic and validation
            var result = await _companyService.UpdateCompanyAsync(companyId, ownerId, request);

            // Use HandleResult to wrap response in the standard ApiResponse envelope
            // { succeeded, message, data } — consistent with all other endpoints
            return HandleResult(result, SuccessCodes.Company.Updated);
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpPut("{companyId}/working-config")]
        public async Task<IActionResult> UpdateWorkingConfig(
            [FromRoute] Guid companyId,
            [FromBody] UpdateWorkingConfigDto request,
            CancellationToken cancellationToken = default)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
            {
                return Unauthorized(CommonErrors.Unauthorized("User is not authenticated."));
            }

            var result = await _companyService.UpdateWorkingConfigAsync(companyId, ownerId, request, cancellationToken);
            return HandleResult(result, "COMPANY_WORKING_CONFIG_UPDATED");
        }

        [Authorize(Roles = "ProjectManager")]
        [HttpGet("{companyId}/working-config")]
        public async Task<IActionResult> GetWorkingConfig(
            [FromRoute] Guid companyId,
            CancellationToken cancellationToken = default)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid ownerId))
            {
                return Unauthorized(CommonErrors.Unauthorized("User is not authenticated."));
            }

            var result = await _companyService.GetWorkingConfigAsync(companyId, ownerId, cancellationToken);
            return HandleResult(result, "COMPANY_WORKING_CONFIG_RETRIEVED");
        }
    }
}