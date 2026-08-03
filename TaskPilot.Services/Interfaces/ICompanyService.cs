using TaskPilot.DTOs.Company;
using TaskPilot.Models.Common.Results;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace TaskPilot.Services.Interfaces
{
    public interface ICompanyService
    {
        Task<Result<CompanyResponse>>
            SetupCompanyAsync(
                SetupCompanyRequest request,
                Guid ownerId);

        Task<Result<InviteEmployeesResponse>>
            InviteEmployeesAsync(
                InviteEmployeesRequest request,
                Guid ownerId);

        Task<Result<List<EmployeeSuggestionDTO>>>
         SearchEmployeesAsync(
           string query, Guid ownerId);

        Task<Result<PagedResult<CompanyInvitationDto>>>
            GetInvitationsAsync(Guid ownerId, TaskPilot.Models.Enums.InvitationStatus status = TaskPilot.Models.Enums.InvitationStatus.All, int page = 1, int pageSize = 20);

        Task<Result<bool>>
            CancelInvitationAsync(Guid invitationId, Guid ownerId);

        Task<Result<bool>>
            ResendInvitationAsync(Guid invitationId, Guid ownerId);

        Task<Result<PagedResult<CompanyEmployeeDto>>>
            GetCompanyEmployeesAsync(
                Guid companyId,
                int page = 1,
                int pageSize = 10,
                bool? isDeactivated = null,
                CancellationToken cancellationToken = default);
        Task<Result<CompanyEmployeeDto>>
            GetCompanyEmployeeByIdAsync(
                Guid companyId,
                string employeeId,
                CancellationToken cancellationToken = default);

        Task<Result<CompanyResponse>>
            UpdateCompanyAsync(
                Guid companyId,
                Guid ownerId,
                UpdateCompanyDto request);
    }
}
