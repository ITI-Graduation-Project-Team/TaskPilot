using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces;

public interface IProjectTeamService
{
    Task<Result<AssignEmployeesResultDto>> AssignEmployeesAsync(
        Guid projectId,
        AssignProjectEmployeesRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<List<ProjectEmployeeDto>>> GetProjectEmployeesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<Result<int>> GetProjectEmployeesCountAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    Task<Result<AssignEmployeesResultDto>> RemoveEmployeeAsync(
        Guid projectId,
        Guid employeeId,
        CancellationToken cancellationToken = default);
}
