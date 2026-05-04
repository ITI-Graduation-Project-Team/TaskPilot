using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

namespace TaskPilot.Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : ApiControllerBase
    {
        private readonly ICvService _cvService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;


        public EmployeeController(ICvService cvService, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
        {
            _cvService = cvService;
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        [HttpPost("upload-cv")]
        [HttpPost("{userId:guid}/upload-cv")]
        public async Task<IActionResult> UploadCv(Guid? userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Invalid file");

            Guid finalUserId;

            if (userId.HasValue)
            {
                if (!User.IsInRole("Admin") && !User.IsInRole("ProjectManager"))
                    return Forbid();

                finalUserId = userId.Value;
            }
            else
            {
                if (_currentUser.UserId == null)
                    return Unauthorized();

                finalUserId = _currentUser.UserId.Value;
            }

            var result = await _cvService.ProcessCvAsync(finalUserId, file);

            if (result.IsSuccess)
                await _unitOfWork.SaveChangesAsync();

            return HandleResult(result, "CV processed successfully.");
        }
    
    }

}
