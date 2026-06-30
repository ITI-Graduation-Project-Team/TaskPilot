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
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out Guid userId))
            {
                return Unauthorized();
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && user.CompanyId != companyId)
            {
                // Let Admin bypass if needed, but per requirement: "Admin (if applicable) can still access"
                if (!User.IsInRole("Admin"))
                {
                    return StatusCode(403, "You do not have access to this company's employees.");
                }
            }

            var result = await _companyService.GetCompanyEmployeesAsync(companyId, cancellationToken);
            return HandleResult(result);
        }
    }
}