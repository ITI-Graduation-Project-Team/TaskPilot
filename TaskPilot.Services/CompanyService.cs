using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs;
using TaskPilot.DTOs.Company;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.External;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;

namespace TaskPilot.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly IRepository<Company>
            _companyRepository;

        private readonly IRepository<ProjectManager>
            _projectManagerRepository;

        private readonly IRepository<Employee>
            _employeeRepository;

        private readonly IRepository<Policy>
            _policyRepository;

        private readonly IRepository<EmployeeInvitation>
            _invitationRepository;

        private readonly IEmailService
            _emailService;

        private readonly IEmailBodyService
            _emailBodyService;

        private readonly IFileStorageService
            _fileStorage;

        private readonly ILocalizationService
            _localizationService;

        public CompanyService(
            IRepository<Company> companyRepository,
            IRepository<ProjectManager>
                projectManagerRepository,
            IRepository<Policy> policyRepository,
            IRepository<EmployeeInvitation>
                invitationRepository,
            IEmailService emailService,
            IEmailBodyService emailBodyService,
            IFileStorageService fileStorage,
            IRepository<Employee> employeeRepository,
            ILocalizationService localizationService)
        {
            _companyRepository =
                companyRepository;

            _projectManagerRepository =
                projectManagerRepository;

            _policyRepository =
                policyRepository;

            _invitationRepository =
                invitationRepository;

            _emailService =
                emailService;

            _emailBodyService =
                emailBodyService;

            _employeeRepository =
                employeeRepository;

            _fileStorage =
                fileStorage;

            _localizationService =
                localizationService;
        }

        public async Task<Result<CompanyResponse>> SetupCompanyAsync(
    SetupCompanyRequest request,
    Guid ownerId)
        {
            // Get Owner

            var owner =
                await _projectManagerRepository
                    .GetByIdAsync(ownerId);

            if (owner is null)
            {
                return Result<CompanyResponse>
                    .Failure(
                        CompanyErrors.InvalidOwner);
            }

            // Prevent creating more than one company

            if (owner.CompanyId.HasValue)
            {
                return Result<CompanyResponse>
                    .Failure(
                        CompanyErrors.ProjectManagerAlreadyHasCompany);
            }

            // Normalize Company Name

            var normalizedCompanyName =
                request.CompanyName
                    .Trim()
                    .ToLower();

            // Check Existing Company

            var companyExists =
                await _companyRepository
                    .AnyAsync(x =>
                        x.Name.ToLower() ==
                        normalizedCompanyName);

            if (companyExists)
            {
                return Result<CompanyResponse>
                    .Failure(
                        CompanyErrors.CompanyAlreadyExists);
            }

            // Create Company

            var company = new Company
            {
                Name = request.CompanyName.Trim(),
                OwnerId = ownerId
            };

            await _companyRepository
                .AddAsync(company);

            // Link Owner To Company

            owner.CompanyId = company.Id;

            // Upload Policy Document

            string? documentUrl = null;
            string? documentPublicId = null;

            if (request.PolicyDocument != null)
            {
                var uploadResult =
                    await _fileStorage
                        .UploadFileAsync(
                            request.PolicyDocument,
                            $"taskpilot/companies/{company.Id}/policies");

                if (!uploadResult.IsSuccess)
                {
                    return Result<CompanyResponse>
                        .Failure(uploadResult.Error!);
                }

                documentUrl =
                    uploadResult.Value.Url;

                documentPublicId =
                    uploadResult.Value.PublicId;
            }

            // Create Default Policy

            if (!string.IsNullOrWhiteSpace(request.PolicyContentEn)
                || !string.IsNullOrWhiteSpace(request.PolicyContentAr)
                || request.PolicyDocument != null)
            {
                var policy = new Policy
                {
                    Scope = PolicyScope.Company,

                    CompanyId = company.Id,

                    TitleEn =
                        request.PolicyTitleEn
                        ?? "General Policy",

                    TitleAr =
                        request.PolicyTitleAr
                        ?? "سياسة عامة",

                    ContentEn =
                        request.PolicyContentEn,

                    ContentAr =
                        request.PolicyContentAr,

                    DocumentUrl =
                        documentUrl,

                    DocumentPublicId =
                        documentPublicId,

                    AiStatus =
                        AiProcessingStatus.Pending,

                    VersionNumber = 1,

                    IsActive = true
                };

                await _policyRepository
                    .AddAsync(policy);
            }

            // Employee Invitations

            request.EmployeeEmails ??=
                new List<string>();

            var emails =
                request.EmployeeEmails
                    .Select(x => x.Trim().ToLower())
                    .Distinct()
                    .ToList();

            foreach (var email in emails)
            {
                var invitationExists =
                    await _invitationRepository
                        .AnyAsync(x =>
                            x.Email == email &&
                            x.CompanyId == company.Id &&
                            !x.IsAccepted);

                if (invitationExists)
                    continue;

                var invitation =
                    new EmployeeInvitation
                    {
                        Email = email,

                        CompanyId = company.Id,

                        InvitedById = ownerId,

                        Token =
                            InvitationTokenGenerator.Generate(),

                        ExpiresAt =
                            DateTime.UtcNow.AddDays(7)
                    };

                await _invitationRepository
                    .AddAsync(invitation);

                var invitationLink =
                    $"https://localhost:4200/accept-invitation?token={invitation.Token}";

                var body =
                    _emailBodyService
                        .GenerateEmployeeInvitationBody(
                            email,
                            company.Name,
                            invitationLink);

                await _emailService
                    .SendEmailAsync(
                        new EmailRequest
                        {
                            To = email,
                            Subject = $"Invitation to join {company.Name}",
                            Body = body
                        });
            }

            var response =
                new CompanyResponse
                {
                    Id = company.Id,
                    Name = company.Name,
                    OwnerId = company.OwnerId
                };

            return Result<CompanyResponse>
                .Success(response);
        }

        public async Task<
            Result<List<EmployeeSuggestionDTO>>>
            SearchEmployeesAsync(
                string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<EmployeeSuggestionDTO>();
            }

            query =
                query.Trim().ToLower();

            var employees =
                await _employeeRepository
                    .FindAsync(e =>

                        e.CompanyId == null

                        &&

                        e.Email != null

                        &&

                        (
                            e.Email.ToLower()
                                .Contains(query)

                            ||

                            e.FirstNameEn
                                .ToLower()
                                .Contains(query)

                            ||

                            e.LastNameEn
                                .ToLower()
                                .Contains(query)
                        ));

            var result =
                employees
                    .Take(10)
                    .Select(e =>
                        new EmployeeSuggestionDTO
                        {
                            Id = e.Id,

                            FullName =
                                $"{_localizationService.GetLocalizedProperty(e.FirstNameEn, e.FirstNameAr)} {_localizationService.GetLocalizedProperty(e.LastNameEn, e.LastNameAr)}".Trim(),

                            Email = e.Email!,

                            HasCompany =
                                e.CompanyId != null
                        })
                    .ToList();

            return result;
        }

        public async Task<Result<List<CompanyEmployeeDto>>> GetCompanyEmployeesAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            var companyExists = await _companyRepository.AnyAsync(c => c.Id == companyId);
            if (!companyExists)
                return Result<List<CompanyEmployeeDto>>.Failure(CompanyErrors.NotFound);

            var employees = await _employeeRepository.GetQueryable()
                .Include(e => e.UserSkills)
                    .ThenInclude(us => us.Skill)
                .Include(e => e.ProjectEmployees)
                    .ThenInclude(pe => pe.Project)
                .Include(e => e.AssignedTasks)
                    .ThenInclude(t => t.Sprint)
                .Where(e => e.CompanyId == companyId)
                .ToListAsync(cancellationToken);

            var dtos = employees.Select(e => {
                var activeProjectsCount = e.ProjectEmployees.Count(pe => pe.Project != null && pe.Project.Status != ProjectStatus.Completed);
                return new CompanyEmployeeDto
                {
                    EmployeeId = e.Id,
                    FullName = $"{_localizationService.GetLocalizedProperty(e.FirstNameEn, e.FirstNameAr)} {_localizationService.GetLocalizedProperty(e.LastNameEn, e.LastNameAr)}".Trim(),
                    JobTitle = e.JobTitle ?? string.Empty,
                    SeniorityLevel = e.SeniorityLevel?.ToString() ?? string.Empty,
                    ActiveProjectsCount = activeProjectsCount,
                    CurrentAssignedTasksCount = e.AssignedTasks.Count(t => t.SprintId != null && t.Status != TaskItemStatus.Done && (t.Sprint == null || t.Sprint.Status == SprintStatus.Active)),
                    AvailabilityStatus = EmployeeAvailabilityHelper.ComputeAvailabilityStatus(activeProjectsCount),
                    Skills = e.UserSkills.Select(us => us.Skill.Name).ToList()
                };
            }).ToList();

            return Result<List<CompanyEmployeeDto>>.Success(dtos);
        }
    }
}