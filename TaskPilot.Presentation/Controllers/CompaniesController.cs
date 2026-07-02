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
        [HttpGet("employees/search")]
        public async Task<ActionResult>
       SearchEmployees(
           [FromQuery] string query)
        {
            var result =
                await _companyService
                    .SearchEmployeesAsync(
                        query);

            return HandleResult(result, SuccessCodes.Company.EmployeesSearched);
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