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

        Task<Result<List<EmployeeSuggestionDTO>>>
         SearchEmployeesAsync(
           string query);

        Task<Result<List<CompanyEmployeeDto>>>
            GetCompanyEmployeesAsync(
                Guid companyId,
                CancellationToken cancellationToken = default);
    }
}
