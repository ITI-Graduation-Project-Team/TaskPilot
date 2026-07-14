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

        public async Task<Result<List<CompanyEmployeeDto>>> GetCompanyEmployeesAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            var companyExists = await _companyRepository.AnyAsync(c => c.Id == companyId);
            if (!companyExists)
                return Result<List<CompanyEmployeeDto>>.Failure(new Error("Company.NotFound", ErrorType.NotFound, "Company not found."));

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
    }
}