using TaskPilot.Models.Common.Results;
using TaskPilot.DTOs.Employees;
using TaskPilot.DTOs.Projects;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace TaskPilot.Services.Interfaces;

public interface IEmployeeDeactivationService
{
    Task<Result<AnalysisResultDto>> AnalyzeDeactivationAsync(Guid employeeId, CancellationToken ct = default);
    Task<Result<ReactivationAnalysisResultDto>> AnalyzeReactivationAsync(Guid employeeId, CancellationToken ct = default);
    Task<Result> DeactivateEmployeeAsync(Guid employeeId, DeactivateEmployeeRequest request, CancellationToken ct = default);
    Task<Result<AssignEmployeesResultDto>> ReactivateEmployeeAsync(Guid employeeId, ReactivateEmployeeRequest request, CancellationToken ct = default);
    Task<Result> TerminateEmployeeAsync(Guid employeeId, TerminateEmployeeRequest request, CancellationToken ct = default);
}
