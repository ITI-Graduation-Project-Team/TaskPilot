using Microsoft.AspNetCore.Authorization;
using TaskPilot.Models.Common;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Presentation.Contracts;
using TaskPilot.Presentation.Controllers;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

[Authorize]
[Route("api/employees")]
[ApiController]
public class EmployeeController : ApiControllerBase
{
    private readonly ICvService _cvService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public EmployeeController(
        ICvService cvService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser
         )
    {
        _cvService = cvService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    // Current logged-in employee uploads CV
    [HttpPost("cv")]
    [HttpPost("{userId:guid}/cv")]
    public async Task<ActionResult> UploadCv(
        Guid? userId,
        [FromForm] UploadCvRequest request)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return HandleResult(Result.Failure<ParsedCvDto>(CvErrors.InvalidFile));
        }
        const long maxFileSize = 5 * 1024 * 1024;

        if (request.File.Length > maxFileSize)
        {
            return HandleResult(Result.Failure<ParsedCvDto>(CvErrors.FileTooLarge));
        }
        var allowedExtensions = new[] { ".pdf", ".docx" };

        var extension = Path
            .GetExtension(request.File.FileName)
            .ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            return HandleResult(Result.Failure<ParsedCvDto>(CvErrors.UnsupportedFormat));
        }

        Guid finalUserId;

        // Admin / PM uploads for another employee
        if (userId.HasValue)
        {
            if (!User.IsInRole("Admin") &&
                !User.IsInRole("ProjectManager"))
            {
                return HandleResult(Result.Failure<ParsedCvDto>(CommonErrors.Forbidden()));
            }

            finalUserId = userId.Value;
        }
        else
        {
            if (_currentUser.UserId == null)
                return HandleResult(Result.Failure<ParsedCvDto>(CommonErrors.Unauthorized()));

            finalUserId = _currentUser.UserId.Value;
        }

        var result = await _cvService
    .ProcessCvAsync(finalUserId, request.File);

        if (result.IsSuccess)
        {
            await _unitOfWork.SaveChangesAsync();

        }

        return HandleResult(result, SuccessCodes.Employee.CvUploaded);
    }
}