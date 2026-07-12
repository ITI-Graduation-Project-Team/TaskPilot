using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces
{
    public class SkillEnrichmentResult
    {
        public int TasksProcessed { get; set; }
        public int TasksEnriched { get; set; }
        public int TasksSkipped { get; set; }
        public int SkillsCreated { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public interface IWbsSkillEnrichmentService
    {
        Task<Result<SkillEnrichmentResult>> EnrichProjectTasksAsync(
            Guid projectId,
            CancellationToken cancellationToken = default);
    }
}
