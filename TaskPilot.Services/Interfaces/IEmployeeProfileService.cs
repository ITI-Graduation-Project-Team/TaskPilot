using TaskPilot.DTOs.Employees;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public interface IEmployeeProfileService
    {
        Task<Result> UpdateProfileAsync(Guid userId, UpdateEmployeeProfileDto request);
    }
}
