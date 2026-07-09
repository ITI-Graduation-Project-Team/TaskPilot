using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Skills;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces;

public interface ISkillMigrationService
{
    Task<Result<SkillMigrationReportDto>> MergeSkillsAsync(SkillMergeRequestDto request, CancellationToken cancellationToken = default);
}
