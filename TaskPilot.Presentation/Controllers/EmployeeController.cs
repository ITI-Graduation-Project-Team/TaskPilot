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
using TaskPilot.DTOs.CV;

[Authorize]
[Route("api/employees")]
[ApiController]
public class EmployeeController : ApiControllerBase
{
    private readonly ICvService _cvService;
    private readonly ICvConfirmationService _cvConfirmationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public EmployeeController(
        ICvService cvService,
        ICvConfirmationService cvConfirmationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser
         )
    {
        _cvService = cvService;
        _cvConfirmationService = cvConfirmationService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Extracts data from an uploaded CV.
    /// </summary>
    /// <remarks>
    /// Note: The returned `IsPrimarySuggested` property is only an AI recommendation.
    /// </remarks>
    [HttpPost("cv/extract")]
    [HttpPost("{userId:guid}/cv/extract")]
    public async Task<ActionResult> ExtractCv(
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

        var result = await _cvService.ExtractAsync(finalUserId, request.File);

        if (result.IsSuccess)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return HandleResult(result, SuccessCodes.Employee.CvUploaded);
    }

    /// <summary>
    /// Confirms and persists the reviewed CV data.
    /// </summary>
    /// <remarks>
    /// Note: The `IsPrimary` property provided here is the employee's final decision.
    /// </remarks>
    [HttpPost("cv/confirm")]
    [HttpPost("{userId:guid}/cv/confirm")]
    public async Task<ActionResult> ConfirmCv(
        Guid? userId,
        [FromBody] ConfirmCvRequest request)
    {
        Guid finalUserId;

        if (userId.HasValue)
        {
            if (!User.IsInRole("Admin") &&
                !User.IsInRole("ProjectManager"))
            {
                return HandleResult(Result.Failure(CommonErrors.Forbidden()));
            }

            finalUserId = userId.Value;
        }
        else
        {
            if (_currentUser.UserId == null)
                return HandleResult(Result.Failure(CommonErrors.Unauthorized()));

            finalUserId = _currentUser.UserId.Value;
        }

        var result = await _cvConfirmationService.ConfirmAsync(finalUserId, request);

        if (result.IsSuccess)
        {
            await _unitOfWork.SaveChangesAsync();
        }

        return HandleResult(result, SuccessCodes.Employee.CvUploaded);
    }
}