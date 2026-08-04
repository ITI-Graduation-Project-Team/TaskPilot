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
using Microsoft.EntityFrameworkCore;
using TaskPilot.Models.Entities;

[Authorize]
[Route("api/employees")]
[ApiController]
public class EmployeeController : ApiControllerBase
{
    private readonly ICvService _cvService;
    private readonly ICvConfirmationService _cvConfirmationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<Employee> _employeeRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Company> _companyRepository;
    private readonly IProjectService _projectService;

    public EmployeeController(
        ICvService cvService,
        ICvConfirmationService cvConfirmationService,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRepository<Employee> employeeRepository,
        IRepository<User> userRepository,
        IRepository<Company> companyRepository,
        IProjectService projectService
         )
    {
        _cvService = cvService;
        _cvConfirmationService = cvConfirmationService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _employeeRepository = employeeRepository;
        _userRepository = userRepository;
        _companyRepository = companyRepository;
        _projectService = projectService;
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

    [Authorize(Roles = "Employee,ProjectManager")]
    [HttpPut("profile")]
    public async Task<ActionResult> UpdateProfile(
        [FromForm] TaskPilot.DTOs.Employees.UpdateEmployeeProfileDto request,
        [FromServices] IEmployeeProfileService profileService,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
            return HandleResult(Result.Failure(CommonErrors.Unauthorized()));

        var result = await profileService.UpdateProfileAsync(_currentUser.UserId.Value, request);
        return HandleResult(result, SuccessCodes.Employee.Updated);
    }

    [HttpGet("profile")]
    public async Task<ActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId == null)
            return HandleResult(Result.Failure(CommonErrors.Unauthorized()));

        var employeeId = _currentUser.UserId.Value;

        var employee = await _employeeRepository.GetQueryable()
            .Include(e => e.UserSkills)
                .ThenInclude(us => us.Skill)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        string companyName = string.Empty;

        if (employee == null)
        {
            var user = await _userRepository.GetQueryable()
                .FirstOrDefaultAsync(u => u.Id == employeeId, cancellationToken);
            if (user == null)
                return HandleResult(Result.Failure(CommonErrors.Unauthorized()));

            string? companyLogoUrl = null;
            if (user.CompanyId.HasValue)
            {
                var company = await _companyRepository.GetByIdAsync(user.CompanyId.Value);
                companyName = company?.Name ?? string.Empty;
                companyLogoUrl = company?.LogoUrl;
            }

            return Ok(new
            {
                Id = user.Id,
                FirstName = user.FirstNameEn,
                LastName = user.LastNameEn,
                FirstNameAr = user.FirstNameAr,
                LastNameAr = user.LastNameAr,
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                Email = user.Email,
                JobTitle = "Project Manager",
                SeniorityLevel = "Manager",
                TotalYearsOfExperience = 0,
                IsEmployee = false,
                CompanyId = user.CompanyId,
                CompanyName = companyName,
                CompanyLogoUrl = companyLogoUrl,
                Skills = new List<object>()
            });
        }

        string? empCompanyLogoUrl = null;
        if (employee.CompanyId.HasValue)
        {
            var company = await _companyRepository.GetByIdAsync(employee.CompanyId.Value);
            companyName = company?.Name ?? string.Empty;
            empCompanyLogoUrl = company?.LogoUrl;
        }

        return Ok(new
        {
            Id = employee.Id,
            FirstName = employee.FirstNameEn,
            LastName = employee.LastNameEn,
            FirstNameAr = employee.FirstNameAr,
            LastNameAr = employee.LastNameAr,
            PhoneNumber = employee.PhoneNumber,
            AvatarUrl = employee.AvatarUrl,
            LatestCvUrl = employee.LatestCvUrl,
            Email = employee.Email,
            JobTitle = employee.JobTitle ?? string.Empty,
            SeniorityLevel = employee.SeniorityLevel?.ToString() ?? "MidLevel",
            TotalYearsOfExperience = employee.TotalYearsOfExperience ?? 0,
            IsEmployee = true,
            CompanyId = employee.CompanyId,
            CompanyName = companyName,
            CompanyLogoUrl = empCompanyLogoUrl,
            Skills = employee.UserSkills.Select(us => new 
            { 
                Name = us.Skill.Name, 
                Level = us.Level.ToString(), 
                YearsOfExperience = us.YearsOfExperience, 
                IsPrimary = us.IsPrimary 
            }).ToList()
        });
    }

    [HttpGet("{employeeId:guid}/projects")]
    public async Task<ActionResult> GetEmployeeProjects(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var result = await _projectService.GetProjectsByEmployeeIdAsync(employeeId, cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Roles = "Admin,ProjectManager")]
    [HttpGet("{employeeId:guid}/deactivation/analyze")]
    public async Task<ActionResult> AnalyzeDeactivation(
        [FromRoute] Guid employeeId,
        [FromServices] IEmployeeDeactivationService deactivationService,
        CancellationToken cancellationToken)
    {
        var result = await deactivationService.AnalyzeDeactivationAsync(employeeId, cancellationToken);
        return HandleResult(result);
    }

    [Authorize(Roles = "Admin,ProjectManager")]
    [HttpPost("{employeeId:guid}/deactivate")]
    public async Task<ActionResult> DeactivateEmployee(
        [FromRoute] Guid employeeId,
        [FromBody] TaskPilot.DTOs.Employees.DeactivateEmployeeRequest request,
        [FromServices] IEmployeeDeactivationService deactivationService,
        CancellationToken cancellationToken)
    {
        var result = await deactivationService.DeactivateEmployeeAsync(employeeId, request, cancellationToken);
        return HandleResult(result);
    }
}