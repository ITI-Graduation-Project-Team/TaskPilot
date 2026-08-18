using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Implementations;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SprintRetrospectiveService : ISprintRetrospectiveService
    {
        private readonly IRepository<SprintRetrospective> _retrospectiveRepository;
        private readonly SprintDataCollectionService _dataCollectionService;
        private readonly SprintRetrospectiveAgent _retrospectiveAgent;

        public SprintRetrospectiveService(
            IRepository<SprintRetrospective> retrospectiveRepository,
            SprintDataCollectionService dataCollectionService,
            SprintRetrospectiveAgent retrospectiveAgent)
        {
            _retrospectiveRepository = retrospectiveRepository;
            _dataCollectionService = dataCollectionService;
            _retrospectiveAgent = retrospectiveAgent;
        }

        public async Task<SprintRetrospectiveDto> GenerateAsync(
            Guid projectId,
            Guid sprintId,
            string userLanguage,
            CancellationToken cancellationToken = default)
        {
            var retrospective = await _retrospectiveRepository.GetQueryable()
                .FirstOrDefaultAsync(sr => sr.SprintId == sprintId, cancellationToken);

            var data = await _dataCollectionService.CollectAsync(sprintId, cancellationToken);

            // A completed sprint is immutable. Reuse its generated AI analysis instead
            // of paying for another model round-trip on every POST request.
            if (HasGeneratedContent(retrospective))
            {
                var cachedAnalysis = DeserializeAnalysis(retrospective!.AnalysisJson);
                var cachedImprovements = DeserializeImprovements(retrospective.ImprovementsJson);

                return MapToDto(retrospective, data, cachedAnalysis, cachedImprovements);
            }

            var (analysis, improvements) = await _retrospectiveAgent.AnalyzeAsync(data, userLanguage, cancellationToken);

            if (retrospective == null)
            {
                retrospective = new SprintRetrospective
                {
                    SprintId = sprintId,
                    GeneratedAt = DateTime.UtcNow
                };

                PopulateEntity(retrospective, data, analysis, improvements);
                await _retrospectiveRepository.AddAsync(retrospective);
            }
            else
            {
                PopulateEntity(retrospective, data, analysis, improvements);
                retrospective.GeneratedAt = DateTime.UtcNow;
                _retrospectiveRepository.Update(retrospective);
            }

            return MapToDto(retrospective, data, analysis, improvements);
        }

        public async Task<SprintRetrospectiveDto?> GetAsync(
            Guid sprintId,
            CancellationToken cancellationToken = default)
        {
            var item = await _retrospectiveRepository.GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(sr => sr.SprintId == sprintId, cancellationToken);

            if (item is null)
                return null;

            var data = await _dataCollectionService.CollectAsync(sprintId, cancellationToken);

            var analysis = DeserializeAnalysis(item.AnalysisJson);
            var improvements = DeserializeImprovements(item.ImprovementsJson);

            return MapToDto(item, data, analysis, improvements);
        }

        private static bool HasGeneratedContent(SprintRetrospective? retrospective)
        {
            return retrospective is not null
                && !string.IsNullOrWhiteSpace(retrospective.AnalysisJson)
                && !string.IsNullOrWhiteSpace(retrospective.ImprovementsJson);
        }

        private static SprintAnalysisDto DeserializeAnalysis(string json)
        {
            return string.IsNullOrWhiteSpace(json)
                ? new SprintAnalysisDto()
                : JsonSerializer.Deserialize<SprintAnalysisDto>(json) ?? new SprintAnalysisDto();
        }

        private static List<SprintImprovementDto> DeserializeImprovements(string json)
        {
            return string.IsNullOrWhiteSpace(json)
                ? new List<SprintImprovementDto>()
                : JsonSerializer.Deserialize<List<SprintImprovementDto>>(json) ?? new List<SprintImprovementDto>();
        }

        private static void PopulateEntity(
            SprintRetrospective entity,
            SprintRetrospectiveData data,
            SprintAnalysisDto analysis,
            List<SprintImprovementDto> improvements)
        {
            entity.CompletionRate = data.CompletionRate;
            entity.VelocityRatio = data.VelocityRatio;
            entity.TotalEstimatedHours = data.TotalEstimatedHours;
            entity.TotalActualHours = data.TotalActualHours;
            entity.TotalTasks = data.TotalTasks;
            entity.CompletedTasks = data.CompletedTasks;
            entity.UnfinishedTasks = data.UnfinishedTasks.Count;

            entity.AnalysisJson = JsonSerializer.Serialize(analysis);
            entity.ImprovementsJson = JsonSerializer.Serialize(improvements);
        }

        private static SprintRetrospectiveDto MapToDto(
            SprintRetrospective entity,
            SprintRetrospectiveData data,
            SprintAnalysisDto analysis,
            List<SprintImprovementDto> improvements)
        {
            var dto = new SprintRetrospectiveDto
            {
                SprintId = entity.SprintId,
                SprintTitleEn = data.SprintTitleEn,
                GeneratedAt = entity.GeneratedAt,
                Metrics = new SprintMetricsDto
                {
                    CompletionRate = entity.CompletionRate,
                    VelocityRatio = entity.VelocityRatio,
                    TotalEstimatedHours = entity.TotalEstimatedHours,
                    TotalActualHours = entity.TotalActualHours,
                    TotalTasks = entity.TotalTasks,
                    CompletedTasks = entity.CompletedTasks,
                    UnfinishedTasks = entity.UnfinishedTasks,
                    DeveloperMetrics = data.DeveloperBreakdowns?.Select(d => new DeveloperMetricDto
                    {
                        EmployeeId = d.EmployeeId,
                        FullName = d.FullName,
                        AssignedTasks = d.AssignedTasks,
                        CompletedTasks = d.CompletedTasks,
                        CompletionRate = d.CompletionRate,
                        VelocityRatio = d.VelocityRatio,
                        EstimatedHours = d.EstimatedHours,
                        ActualHours = d.ActualHours
                    }).ToList() ?? new List<DeveloperMetricDto>()
                },
                Analysis = analysis,
                Improvements = improvements
            };
            return dto;
        }
    }
}
