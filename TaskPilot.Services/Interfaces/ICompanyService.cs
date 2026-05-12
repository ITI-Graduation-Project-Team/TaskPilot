using TaskPilot.DTOs.Company;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface ICompanyService
    {
        Task<Result<CompanyResponse>>
            SetupCompanyAsync(
                SetupCompanyRequest request,
                Guid ownerId);
    }
}
