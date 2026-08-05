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

        private readonly IRepository<User>
            _userRepository;

        private readonly ICompanyPolicyService
            _companyPolicyService;

        private readonly IRepository<EmployeeInvitation>
            _invitationRepository;

        private readonly IEmailService
            _emailService;

        private readonly IEmailBodyService
            _emailBodyService;

        private readonly IFileStorageService
            _fileStorage;

        private readonly IUnitOfWork
            _unitOfWork;

        public CompanyService(
            IRepository<Company> companyRepository,
            IRepository<ProjectManager>
                projectManagerRepository,
            ICompanyPolicyService companyPolicyService,
            IRepository<EmployeeInvitation>
                invitationRepository,
            IEmailService emailService,
            IEmailBodyService emailBodyService,
            IFileStorageService fileStorage,
            IRepository<Employee> employeeRepository,
            IRepository<User> userRepository,
            IUnitOfWork unitOfWork)
        {
            _companyRepository =
                companyRepository;

            _projectManagerRepository =
                projectManagerRepository;

            _companyPolicyService =
                companyPolicyService;

            _invitationRepository =
                invitationRepository;

            _emailService =
                emailService;

            _emailBodyService =
                emailBodyService;

            _employeeRepository =
                employeeRepository;

            _userRepository =
                userRepository;

            _fileStorage =
                fileStorage;

            _unitOfWork =
                unitOfWork;
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

            // Create Company

            var company = new Company
            {
                Name = request.CompanyName.Trim(),
                OwnerId = ownerId,
                LogoUrl = $"https://api.dicebear.com/9.x/initials/svg?seed={Uri.EscapeDataString(request.CompanyName.Trim())}"
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
                var ingestRequest = new TaskPilot.DTOs.AI.CompanyPolicies.IngestCompanyPolicyRequest
                {
                    CompanyId = company.Id,
                    File = request.PolicyDocument,
                    TitleEn = request.PolicyTitleEn ?? "General Policy",
                    TitleAr = request.PolicyTitleAr ?? "سياسة عامة",
                    ContentEn = request.PolicyContentEn,
                    ContentAr = request.PolicyContentAr,
                    DocumentUrl = documentUrl,
                    CloudinaryPublicId = documentPublicId,
                    SkipCloudUpload = true
                };

                var ingestResult = await _companyPolicyService.IngestAsync(ingestRequest, ct => Task.CompletedTask);
                if (!ingestResult.IsSuccess)
                {
                    return Result<CompanyResponse>.Failure(ingestResult.Error!);
                }
            }

            // Employee Invitations — save all DB records first, then send emails

            request.EmployeeEmails ??= new List<string>();

            var emails =
                request.EmployeeEmails
                    .Select(x => x.Trim().ToLower())
                    .Distinct()
                    .ToList();

            // Collect valid invitations to send in memory before saving
            var pendingInvitations = new List<(string email, EmployeeInvitation invitation)>();

            foreach (var email in emails)
            {
                var normalizedEmail = email.Trim().ToLower();

                var alreadyInCompany = await _employeeRepository
                    .AnyAsync(x => x.Email == normalizedEmail && x.CompanyId == company.Id);
                if (alreadyInCompany) continue;

                var existingUser = await _userRepository
                    .FindSingleAsync(x => x.Email == normalizedEmail);
                if (existingUser != null && existingUser.CompanyId.HasValue) continue;

                var invitationExists = await _invitationRepository
                    .AnyAsync(x => x.Email == normalizedEmail && x.CompanyId == company.Id && !x.IsAccepted);
                if (invitationExists) continue;

                var invitation = new EmployeeInvitation
                {
                    Email = normalizedEmail,
                    CompanyId = company.Id,
                    InvitedById = ownerId,
                    Token = InvitationTokenGenerator.Generate(),
                    ExpiresAt = DateTime.UtcNow.AddDays(7)
                };

                await _invitationRepository.AddAsync(invitation);
                pendingInvitations.Add((normalizedEmail, invitation));
            }

            // Flush all invitation records to DB before sending emails
            if (pendingInvitations.Count > 0)
            {
                await _unitOfWork.SaveChangesAsync();

                foreach (var (email, invitation) in pendingInvitations)
                {
                    var invitationLink = $"http://localhost:4200/accept-invitation?token={invitation.Token}";
                    var body = _emailBodyService.GenerateEmployeeInvitationBody(email, company.Name, invitationLink);
                    await _emailService.SendEmailAsync(new EmailRequest
                    {
                        To = email,
                        Subject = $"You're invited to join {company.Name} on TaskPilot",
                        Body = body
                    });
                }
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
                string query, Guid ownerId)
        {
            var owner = await _projectManagerRepository.GetByIdAsync(ownerId);
            if (owner is null) return Result<List<EmployeeSuggestionDTO>>.Failure(CompanyErrors.InvalidOwner);
            if (owner.CompanyId is null) return Result<List<EmployeeSuggestionDTO>>.Failure(CompanyErrors.NotFound);

            var companyId = owner.CompanyId.Value;

            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<EmployeeSuggestionDTO>();
            }

            query = query.Trim().ToLower();

            var employees = await _employeeRepository.FindAsync(e =>
                e.Email != null &&
                (e.Email.ToLower().Contains(query) ||
                 e.FirstNameEn.ToLower().Contains(query) ||
                 e.LastNameEn.ToLower().Contains(query) ||
                 (e.FirstNameEn + " " + e.LastNameEn).ToLower().Contains(query)));

            var result = new List<EmployeeSuggestionDTO>();
            foreach (var e in employees.Take(10))
            {
                var status = EmployeeSearchStatus.Available;
                string? statusMessage = "Ready to invite.";

                if (e.CompanyId != null)
                {
                    status = EmployeeSearchStatus.AlreadyInCompany;
                    statusMessage = "Already belongs to another company.";
                }
                else
                {
                    var isPending = await _invitationRepository.AnyAsync(x => x.Email == e.Email && !x.IsAccepted);
                    if (isPending)
                    {
                        status = EmployeeSearchStatus.PendingInvitation;
                        statusMessage = "Invitation already pending.";
                    }
                }

                result.Add(new EmployeeSuggestionDTO
                {
                    Id = e.Id,
                    FullName = $"{e.FirstNameEn} {e.LastNameEn}",
                    Email = e.Email!,
                    Status = status,
                    StatusMessage = statusMessage
                });
            }

            return result;
        }

        public async Task<Result<PagedResult<CompanyEmployeeDto>>> GetCompanyEmployeesAsync(
            Guid companyId,
            int page = 1,
            int pageSize = 10,
            bool? isDeactivated = null,
            CancellationToken cancellationToken = default)
        {
            var companyExists = await _companyRepository.AnyAsync(c => c.Id == companyId);
            if (!companyExists)
                return Result<PagedResult<CompanyEmployeeDto>>.Failure(new Error("Company.NotFound", ErrorType.NotFound, "Company not found."));

            var query = _employeeRepository.GetQueryable()
                .Where(e => e.CompanyId == companyId);

            if (isDeactivated.HasValue)
            {
                query = query.Where(e => e.IsDeactivated == isDeactivated.Value);
            }

            var totalItems = await query.CountAsync(cancellationToken);
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

            var employees = await query
                .Include(e => e.UserSkills)
                    .ThenInclude(us => us.Skill)
                .Include(e => e.ProjectEmployees)
                    .ThenInclude(pe => pe.Project)
                .Include(e => e.AssignedTasks)
                    .ThenInclude(t => t.Sprint)
                .AsSplitQuery()
                .OrderBy(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var dtos = employees.Select(e => {
                var activeProjectsCount = e.ProjectEmployees.Count(pe => pe.Project != null && pe.Project.Status != ProjectStatus.Completed);
                var fullName = $"{e.FirstNameEn} {e.LastNameEn}".Trim();
                if (string.IsNullOrEmpty(fullName))
                {
                    fullName = e.Email ?? string.Empty;
                }
                return new CompanyEmployeeDto
                {
                    EmployeeId = e.Id,
                    FullName = fullName,
                    Email = e.Email ?? string.Empty,
                    AvatarUrl = e.AvatarUrl,
                    JobTitle = e.JobTitle ?? string.Empty,
                    SeniorityLevel = e.SeniorityLevel?.ToString() ?? string.Empty,
                    ActiveProjectsCount = activeProjectsCount,
                    CurrentAssignedTasksCount = e.AssignedTasks.Count(t => t.SprintId != null && t.Status != TaskItemStatus.Done && (t.Sprint == null || t.Sprint.Status == SprintStatus.Active)),
                    AvailabilityStatus = EmployeeAvailabilityHelper.ComputeAvailabilityStatus(activeProjectsCount),
                    Skills = e.UserSkills.Select(us => us.Skill.Name).ToList(),
                    IsDeactivated = e.IsDeactivated,
                    DeactivationReason = e.DeactivationReason,
                    DeactivatedAt = e.DeactivatedAt
                };
            }).ToList();

            var pagedResult = new PagedResult<CompanyEmployeeDto>
            {
                Items = dtos,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return Result<PagedResult<CompanyEmployeeDto>>.Success(pagedResult);
        }
        public async Task<Result<InviteEmployeesResponse>> InviteEmployeesAsync(
            InviteEmployeesRequest request,
            Guid ownerId)
        {
            var owner = await _projectManagerRepository.GetByIdAsync(ownerId);
            if (owner is null)
                return Result<InviteEmployeesResponse>.Failure(CompanyErrors.InvalidOwner);

            if (owner.CompanyId is null)
                return Result<InviteEmployeesResponse>.Failure(CompanyErrors.NotFound);

            if (request.Emails is null || !request.Emails.Any())
                return Result<InviteEmployeesResponse>.Failure(CompanyErrors.NoEmployeesSpecified);

            var company = await _companyRepository.GetByIdAsync(owner.CompanyId.Value);
            if (company is null)
                return Result<InviteEmployeesResponse>.Failure(CompanyErrors.NotFound);

            var emails = request.Emails
                .Where(e => EmailValidator.IsValid(e))
                .Select(e => e.Trim().ToLower())
                .Distinct()
                .ToList();

            var response = new InviteEmployeesResponse();

            foreach (var email in emails)
            {
                var result = await CreateInvitationAsync(email, company, ownerId);
                
                if (result.IsSuccess)
                {
                    response.InvitedEmails.Add(email);
                    response.SentCount++;
                }
                else
                {
                    response.SkippedEmployees.Add(new SkippedEmployeeDto
                    {
                        Email = email,
                        Reason = result.Error?.Code ?? "UnknownError"
                    });
                }
            }

            var originalEmails = request.Emails.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            var processedSet = new HashSet<string>();

            foreach (var email in originalEmails)
            {
                var normalized = email.Trim().ToLower();
                if (!EmailValidator.IsValid(normalized))
                {
                    response.SkippedEmployees.Add(new SkippedEmployeeDto
                    {
                        Email = email,
                        Reason = "INVALID_EMAIL"
                    });
                    continue;
                }

                if (!processedSet.Add(normalized))
                {
                    response.SkippedEmployees.Add(new SkippedEmployeeDto
                    {
                        Email = email,
                        Reason = "DUPLICATE_IN_REQUEST"
                    });
                }
            }

            return Result<InviteEmployeesResponse>.Success(response);
        }

        private async Task<Result<bool>> CreateInvitationAsync(
            string email,
            Company company,
            Guid ownerId)
        {
            var normalizedEmail = email.Trim().ToLower();

            var alreadyInCompany = await _employeeRepository
                .AnyAsync(x => x.Email == normalizedEmail && x.CompanyId == company.Id);

            if (alreadyInCompany)
                return Result<bool>.Failure(CompanyErrors.EmployeeAlreadyInCompany);

            var existingUser = await _userRepository
                .FindSingleAsync(x => x.Email == normalizedEmail);

            if (existingUser != null && existingUser.CompanyId.HasValue)
                return Result<bool>.Failure(CompanyErrors.UserAlreadyBelongsToCompany);

            var invitationExists = await _invitationRepository
                .AnyAsync(x =>
                    x.Email == normalizedEmail &&
                    x.CompanyId == company.Id &&
                    !x.IsAccepted);

            if (invitationExists)
                return Result<bool>.Failure(CompanyErrors.InvitationAlreadySent);

            var invitation = new EmployeeInvitation
            {
                Email = normalizedEmail,
                CompanyId = company.Id,
                InvitedById = ownerId,
                Token = InvitationTokenGenerator.Generate(),
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            await _invitationRepository.AddAsync(invitation);

            var invitationLink = $"http://localhost:4200/accept-invitation?token={invitation.Token}";

            var body = _emailBodyService.GenerateEmployeeInvitationBody(
                normalizedEmail,
                company.Name,
                invitationLink);

            var emailResult = await _emailService.SendEmailAsync(new EmailRequest
            {
                To = normalizedEmail,
                Subject = $"Invitation to join {company.Name}",
                Body = body
            });

            if (emailResult.IsFailure)
            {
                return Result<bool>.Failure(emailResult.Error!);
            }

            return Result<bool>.Success(true);
        }

        public async Task<Result<PagedResult<CompanyInvitationDto>>> GetInvitationsAsync(Guid ownerId, InvitationStatus status = InvitationStatus.All, int page = 1, int pageSize = 20)
        {
            var owner = await _projectManagerRepository.GetByIdAsync(ownerId);
            if (owner is null) return Result<PagedResult<CompanyInvitationDto>>.Failure(CompanyErrors.InvalidOwner);
            if (owner.CompanyId is null) return Result<PagedResult<CompanyInvitationDto>>.Failure(CompanyErrors.NotFound);

            var companyId = owner.CompanyId.Value;
            var utcNow = DateTime.UtcNow;

            var query = _invitationRepository.GetQueryable().Where(x => x.CompanyId == companyId &&
                (
                    status == InvitationStatus.All ||
                    (status == InvitationStatus.Pending && !x.IsAccepted && x.ExpiresAt > utcNow) ||
                    (status == InvitationStatus.Accepted && x.IsAccepted) ||
                    (status == InvitationStatus.Expired && !x.IsAccepted && x.ExpiresAt <= utcNow)
                ));

            var totalItems = await query.CountAsync();
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

            var invitations = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = invitations.Select(i => new CompanyInvitationDto
            {
                Id = i.Id,
                Email = i.Email,
                InvitedAt = i.CreatedAt,
                ExpiresAt = i.ExpiresAt,
                Accepted = i.IsAccepted,
                InvitedBy = owner.FirstNameEn + " " + owner.LastNameEn
            }).ToList();

            var pagedResult = new PagedResult<CompanyInvitationDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };

            return Result<PagedResult<CompanyInvitationDto>>.Success(pagedResult);
        }

        public async Task<Result<bool>> CancelInvitationAsync(Guid invitationId, Guid ownerId)
        {
            var owner = await _projectManagerRepository.GetByIdAsync(ownerId);
            if (owner is null) return Result<bool>.Failure(CompanyErrors.InvalidOwner);
            if (owner.CompanyId is null) return Result<bool>.Failure(CompanyErrors.NotFound);

            var invitation = await _invitationRepository.GetByIdAsync(invitationId);
            if (invitation == null || invitation.CompanyId != owner.CompanyId.Value)
            {
                return Result<bool>.Failure(CommonErrors.NotFound("Invitation"));
            }

            if (invitation.IsAccepted)
            {
                return Result<bool>.Failure(CompanyErrors.InvitationAlreadyAccepted);
            }

            _invitationRepository.Delete(invitation);
            return Result<bool>.Success(true);
        }

        public async Task<Result<bool>> ResendInvitationAsync(Guid invitationId, Guid ownerId)
        {
            var owner = await _projectManagerRepository.GetByIdAsync(ownerId);
            if (owner is null) return Result<bool>.Failure(CompanyErrors.InvalidOwner);
            if (owner.CompanyId is null) return Result<bool>.Failure(CompanyErrors.NotFound);

            var invitation = await _invitationRepository.GetByIdAsync(invitationId);
            if (invitation == null || invitation.CompanyId != owner.CompanyId.Value)
            {
                return Result<bool>.Failure(CommonErrors.NotFound("Invitation"));
            }

            if (invitation.IsAccepted)
            {
                return Result<bool>.Failure(CompanyErrors.InvitationAlreadyAccepted);
            }

            if (invitation.ExpiresAt < DateTime.UtcNow.AddDays(-30))
            {
                return Result<bool>.Failure(CompanyErrors.InvitationExpired);
            }

            var company = await _companyRepository.GetByIdAsync(owner.CompanyId.Value);

            var invitationLink = $"https://localhost:4200/accept-invitation?token={invitation.Token}";
            var body = _emailBodyService.GenerateEmployeeInvitationBody(invitation.Email, company!.Name, invitationLink);

            await _emailService.SendEmailAsync(new EmailRequest
            {
                To = invitation.Email,
                Subject = $"Invitation to join {company.Name}",
                Body = body
            });

            return Result<bool>.Success(true);
        }

        public async Task<Result<CompanyEmployeeDto>> GetCompanyEmployeeByIdAsync(
            Guid companyId,
            string employeeId,
            CancellationToken cancellationToken = default)
        {
            var companyExists = await _companyRepository.AnyAsync(c => c.Id == companyId);
            if (!companyExists)
                return Result<CompanyEmployeeDto>.Failure(new Error("Company.NotFound", ErrorType.NotFound, "Company not found."));

            if (!Guid.TryParse(employeeId, out Guid empGuid))
                return Result<CompanyEmployeeDto>.Failure(new Error("Employee.InvalidId", ErrorType.Validation, "Invalid employee ID format."));

            var e = await _employeeRepository.GetQueryable()
                .Where(x => x.CompanyId == companyId && x.Id == empGuid)
                .Include(x => x.UserSkills)
                    .ThenInclude(us => us.Skill)
                .Include(x => x.ProjectEmployees)
                    .ThenInclude(pe => pe.Project)
                .Include(x => x.AssignedTasks)
                    .ThenInclude(t => t.Sprint)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);

            if (e == null)
            {
                return Result<CompanyEmployeeDto>.Failure(new Error("Employee.NotFound", ErrorType.NotFound, "Employee not found."));
            }

            var activeProjectsCount = e.ProjectEmployees.Count(pe => pe.Project != null && pe.Project.Status != ProjectStatus.Completed);
            var fullName = $"{e.FirstNameEn} {e.LastNameEn}".Trim();
            if (string.IsNullOrEmpty(fullName))
            {
                fullName = e.Email ?? string.Empty;
            }

            var dto = new CompanyEmployeeDto
            {
                EmployeeId = e.Id,
                FullName = fullName,
                Email = e.Email ?? string.Empty,
                AvatarUrl = e.AvatarUrl,
                JobTitle = e.JobTitle ?? string.Empty,
                SeniorityLevel = e.SeniorityLevel?.ToString() ?? string.Empty,
                ActiveProjectsCount = activeProjectsCount,
                CurrentAssignedTasksCount = e.AssignedTasks.Count(t => t.SprintId != null && t.Status != TaskItemStatus.Done && (t.Sprint == null || t.Sprint.Status == SprintStatus.Active)),
                AvailabilityStatus = EmployeeAvailabilityHelper.ComputeAvailabilityStatus(activeProjectsCount),
                Skills = e.UserSkills.Select(us => us.Skill.Name).ToList(),
                IsDeactivated = e.IsDeactivated,
                DeactivationReason = e.DeactivationReason,
                DeactivatedAt = e.DeactivatedAt
            };

            return Result<CompanyEmployeeDto>.Success(dto);
        }

        public async Task<Result<CompanyResponse>> UpdateCompanyAsync(
            Guid companyId,
            Guid ownerId,
            UpdateCompanyDto request)
        {
            // 1. Fetch the company by ID
            var company = await _companyRepository.GetByIdAsync(companyId);
            if (company == null)
            {
                return Result<CompanyResponse>.Failure(CompanyErrors.NotFound);
            }

            // 2. Authorization check — only the owner can update the company
            if (company.OwnerId != ownerId)
            {
                return Result<CompanyResponse>.Failure(CompanyErrors.InvalidOwner);
            }

            // 3. Update the company name
            company.Name = request.Name.Trim();

            // 4. Handle logo upload or removal
            if (request.RemoveLogo)
            {
                company.LogoUrl = null;
                company.CloudinaryPublicId = null;
            }
            else if (request.Logo != null && request.Logo.Length > 0)
            {
                // Upload the new logo to Cloudinary
                var uploadResult = await _fileStorage.UploadFileAsync(
                    request.Logo,
                    $"taskpilot/companies/{company.Id}/logos");

                if (!uploadResult.IsSuccess)
                {
                    return Result<CompanyResponse>.Failure(uploadResult.Error!);
                }

                company.LogoUrl = uploadResult.Value.Url;
                company.CloudinaryPublicId = uploadResult.Value.PublicId;
            }

            // 5. No logo uploaded — generate or update avatar if there's no custom logo
            if (string.IsNullOrEmpty(company.LogoUrl) || company.LogoUrl.StartsWith("https://ui-avatars.com") || company.LogoUrl.StartsWith("https://api.dicebear.com"))
            {
                // Use DiceBear initials which has better support for Arabic and Unicode characters than ui-avatars
                company.LogoUrl = $"https://api.dicebear.com/9.x/initials/svg?seed={Uri.EscapeDataString(company.Name)}";
            }

            // 6. Persist changes
            company.ModifiedAt = DateTime.UtcNow;
            _companyRepository.Update(company);
            await _unitOfWork.SaveChangesAsync();

            // 7. Return the updated company details
            var response = new CompanyResponse
            {
                Id = company.Id,
                Name = company.Name,
                OwnerId = company.OwnerId,
                LogoUrl = company.LogoUrl
            };

            return Result<CompanyResponse>.Success(response);
        }

        public async Task<Result<bool>> UpdateWorkingConfigAsync(
            Guid companyId,
            Guid ownerId,
            UpdateWorkingConfigDto request,
            CancellationToken cancellationToken = default)
        {
            var company = await _companyRepository.GetByIdAsync(companyId);
            if (company == null)
            {
                return Result<bool>.Failure(CompanyErrors.NotFound);
            }

            if (company.OwnerId != ownerId)
            {
                return Result<bool>.Failure(CompanyErrors.InvalidOwner);
            }

            company.WorkingHoursPerDay = request.WorkingHoursPerDay;
            company.WorkingDaysMask = request.WorkingDaysMask;
            company.DefaultCapacityBufferPercentage = request.DefaultCapacityBufferPercentage;
            company.ModifiedAt = DateTime.UtcNow;

            _companyRepository.Update(company);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<bool>.Success(true);
        }
    }
}