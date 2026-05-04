using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : ApiControllerBase
    {
        private readonly ICvService _cvService;
        private readonly IUnitOfWork _unitOfWork;

        public EmployeeController(ICvService cvService, IUnitOfWork unitOfWork)
        {
            _cvService = cvService;
            _unitOfWork = unitOfWork;
        }
        [HttpPost("{userId:guid}/upload-cv")]
        public async Task<IActionResult> UploadCv(Guid userId, IFormFile file)
        {
            // 🟢 1. Validate file
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file");

            // 🟢 2. Call service
            var result = await _cvService.ProcessCvAsync(userId, file);

            // 🟢 3. Save changes (UnitOfWork)
            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            // 🟢 4. Return standardized response
            return HandleResult(result, "CV processed successfully.");
        }
    }
}
