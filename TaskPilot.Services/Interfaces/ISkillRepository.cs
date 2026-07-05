using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Models.Planning;

namespace TaskPilot.Services.Interfaces
{
    public interface ISkillRepository
    {
        Task<List<EmployeeSkillSummary>> GetCompanySkillSummaryAsync(
            Guid companyId,
            CancellationToken cancellationToken = default);
        Task<List<EmployeeSkillSummary>> GetProjectSkillSummaryAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);
    }
}
