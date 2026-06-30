using Microsoft.AspNetCore.Authorization;
using TaskPilot.Models.Common;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Company;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Presentation.Controllers
{
    //[Authorize]
    [Route("api/companies")]
    public class CompaniesController
        : ApiControllerBase
    {
        private readonly ICompanyService
            _companyService;

        private readonly IUnitOfWork
            _unitOfWork;

        public CompaniesController(
            ICompanyService companyService,
            IUnitOfWork unitOfWork)
        {
            _companyService =
                companyService;

            _unitOfWork =
                unitOfWork;
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
    }
}