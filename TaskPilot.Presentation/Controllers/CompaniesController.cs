using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Company;
using TaskPilot.Services.Interfaces;

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
                [FromBody]
                SetupCompanyRequest request)
            {
            var userId =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier);

            if (!Guid.TryParse(
                    userId,
                    out Guid ownerId))
            {
                return Unauthorized();
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

            return HandleCreated(
                result,
                "Company setup completed successfully.");
        }
    }
}