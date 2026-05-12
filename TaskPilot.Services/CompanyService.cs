using TaskPilot.Data.Repositories;
using TaskPilot.DTOs;
using TaskPilot.DTOs.Company;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly IRepository<Company>
            _companyRepository;

        private readonly IRepository<ProjectManager>
            _projectManagerRepository;

        private readonly IRepository<Policy>
            _policyRepository;

        private readonly IRepository<EmployeeInvitation>
            _invitationRepository;

        private readonly IEmailService
            _emailService;

        private readonly IEmailBodyService
            _emailBodyService;

        public CompanyService(
            IRepository<Company> companyRepository,
            IRepository<ProjectManager>
                projectManagerRepository,
            IRepository<Policy> policyRepository,
            IRepository<EmployeeInvitation>
                invitationRepository,
            IEmailService emailService,
            IEmailBodyService emailBodyService)
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
        }

        public async Task<Result<CompanyResponse>>
            SetupCompanyAsync(
                SetupCompanyRequest request,
                Guid ownerId)
        {
            // Validate Owner

            var ownerExists =
                await _projectManagerRepository
                    .AnyAsync(x => x.Id == ownerId);

            if (!ownerExists)
            {
                return Result<CompanyResponse>
                    .Failure(
                        CompanyErrors.InvalidOwner);
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
                        x.Name.ToLower()
                        == normalizedCompanyName);

            if (companyExists)
            {
                return Result<CompanyResponse>
                    .Failure(
                        CompanyErrors
                            .CompanyAlreadyExists);
            }

            // Create Company

            var company = new Company
            {
                Name = request.CompanyName.Trim(),

                OwnerId = ownerId
            };

            await _companyRepository
                .AddAsync(company);

            // Create Default Policy

            if (
                !string.IsNullOrWhiteSpace(
                    request.PolicyContentEn)
                ||
                !string.IsNullOrWhiteSpace(
                    request.PolicyContentAr)
                ||
                !string.IsNullOrWhiteSpace(
                    request.PolicyDocumentUrl)
            )
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
                        request.PolicyDocumentUrl,

                    AiStatus =
                        AiProcessingStatus.Pending,

                    VersionNumber = 1,

                    IsActive = true
                };

                await _policyRepository
                    .AddAsync(policy);
            }

            // Employee Invitations

            if (request.EmployeeEmails is null)
            {
                request.EmployeeEmails =
                    new List<string>();
            }

            var emails =
                request.EmployeeEmails
                    .Select(x =>
                        x.Trim().ToLower())
                    .Distinct()
                    .ToList();

            foreach (var email in emails)
            {
                // Check Existing Pending Invitation

                var invitationExists =
                    await _invitationRepository
                        .AnyAsync(x =>
                            x.Email == email
                            &&
                            x.CompanyId
                                == company.Id
                            &&
                            !x.IsAccepted);

                if (invitationExists)
                    continue;

                // Create Invitation

                var invitation =
                    new EmployeeInvitation
                    {
                        Email = email,

                        CompanyId = company.Id,

                        InvitedById = ownerId,

                        Token =
                            InvitationTokenGenerator
                           .Generate(),

                        ExpiresAt =
                            DateTime.UtcNow
                                .AddDays(7)
                    };

                await _invitationRepository
                    .AddAsync(invitation);

                // Invitation Link

                var invitationLink =
                    $"https://localhost:4200/" +
                    $"accept-invitation" +
                    $"?token={invitation.Token}";

                // Generate Email Body

                var body =
                    _emailBodyService
                        .GenerateEmployeeInvitationBody(
                            email,
                            company.Name,
                            invitationLink);

                // Send Email

                await _emailService
                    .SendEmailAsync(
                        new EmailRequest
                        {
                            To = email,

                            Subject =
                                $"Invitation to join {company.Name}",

                            Body = body
                        });
            }

            // Response

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
    }
}