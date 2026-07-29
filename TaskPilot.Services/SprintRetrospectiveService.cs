using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;
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

            if (sprint.Status != SprintStatus.Completed)
            {
                return Result.Failure<SprintRetrospectiveResponseDto>(
                    CommonErrors.InvalidInput("A retrospective report can be generated only for a completed sprint."));
            }

            var existing = await retrospectiveRepository.FindAsync(sr => sr.SprintId == sprintId);
            if (existing.Any())
            {
                return Result.Success(MapToDto(existing.First()));
            }

            var existing = await _retrospectiveRepository.FindAsync(sr => sr.SprintId == sprintId);
            var retrospective = existing.FirstOrDefault();

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
            var items = await _retrospectiveRepository.FindAsync(sr => sr.SprintId == sprintId);
            var item = items.FirstOrDefault();
            
            if (item is null)
                return null;
                
            var analysis = string.IsNullOrEmpty(item.AnalysisJson) 
                ? new SprintAnalysisDto() 
                : JsonSerializer.Deserialize<SprintAnalysisDto>(item.AnalysisJson) ?? new SprintAnalysisDto();
                
            var improvements = string.IsNullOrEmpty(item.ImprovementsJson) 
                ? new List<SprintImprovementDto>() 
                : JsonSerializer.Deserialize<List<SprintImprovementDto>>(item.ImprovementsJson) ?? new List<SprintImprovementDto>();

            var retrospective = new SprintRetrospective
            {
                SprintId = sprintId,
                WhatWentWellEn = aiResult.WhatWentWellEn,
                WhatWentWellAr = aiResult.WhatWentWellAr,
                ChallengesEn = aiResult.ChallengesEn,
                ChallengesAr = aiResult.ChallengesAr,
                ActionItemsEn = aiResult.ActionItemsEn,
                ActionItemsAr = aiResult.ActionItemsAr,
                CompletionRate = completionRate,
                EstimationAccuracy = accuracy,
                ExpectedHours = expectedHours,
                ActualHours = actualHours,
                TeamSentimentSummaryEn = aiResult.TeamSentimentSummaryEn,
                TeamSentimentSummaryAr = aiResult.TeamSentimentSummaryAr
            };

            await retrospectiveRepository.AddAsync(retrospective);

            return Result.Success(MapToDto(retrospective));
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
                        FullName = d.FullName,
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
