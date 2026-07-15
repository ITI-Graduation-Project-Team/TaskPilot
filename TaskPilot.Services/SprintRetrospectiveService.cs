using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SprintRetrospectiveService(
        IRepository<Sprint> sprintRepository,
        IRepository<SprintRetrospective> retrospectiveRepository,
        SprintRetrospectiveAgent retrospectiveAgent) : ISprintRetrospectiveService
    {
        public async Task<Result<SprintRetrospectiveResponseDto>> GenerateRetrospectiveAsync(Guid sprintId, CancellationToken cancellationToken = default)
        {
            var sprint = await sprintRepository.GetQueryable()
                .Include(s => s.Tasks)
                    .ThenInclude(t => t.Comments)
                .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken);

            if (sprint is null)
            {
                return Result.Failure<SprintRetrospectiveResponseDto>(CommonErrors.NotFound("Sprint"));
            }

            //if (sprint.Status != SprintStatus.Completed)
            //{
            //    return Result.Failure<SprintRetrospectiveResponseDto>(
            //        CommonErrors.InvalidInput("A retrospective report can be generated only for a completed sprint."));
            //}

            var existing = await retrospectiveRepository.FindAsync(sr => sr.SprintId == sprintId);
            if (existing.Any())
            {
                return Result.Success(MapToDto(existing.First()));
            }

            var completedTasks = sprint.Tasks.Where(t => t.Status == TaskItemStatus.Done).ToList();
            var delayedTasks = sprint.Tasks.Where(t => t.Status != TaskItemStatus.Done).ToList();

            var completedTasksData = completedTasks.Select(t => new
            {
                t.TitleEn,
                Estimated = t.EstimatedHours,
                Actual = t.ActualHours
            }).ToList();

            var delayedTasksData = delayedTasks.Select(t => new
            {
                t.TitleEn,
                Status = t.Status.ToString(),
                Estimated = t.EstimatedHours
            }).ToList();

            var commentsData = sprint.Tasks
                .SelectMany(t => t.Comments)
                .Select(c => new { c.ContentEn, c.ContentAr })
                .ToList();

            var aiResult = await retrospectiveAgent.AnalyzeSprintAsync(
                sprint.SprintGoalEn ?? string.Empty,
                JsonSerializer.Serialize(completedTasksData),
                JsonSerializer.Serialize(delayedTasksData),
                JsonSerializer.Serialize(commentsData),
                cancellationToken);

            double completionRate = sprint.Tasks.Any() 
                ? (double)completedTasks.Count / sprint.Tasks.Count * 100 
                : 0.0;

            decimal totalEstimated = completedTasks.Sum(t => t.EstimatedHours);
            decimal totalActual = completedTasks.Sum(t => t.ActualHours);
            decimal accuracy = totalActual > 0 ? (totalEstimated / totalActual) * 100 : 100;

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
                TeamSentimentSummaryEn = aiResult.TeamSentimentSummaryEn,
                TeamSentimentSummaryAr = aiResult.TeamSentimentSummaryAr
            };

            await retrospectiveRepository.AddAsync(retrospective);

            return Result.Success(MapToDto(retrospective));
        }

        public async Task<Result<SprintRetrospectiveResponseDto>> GetRetrospectiveAsync(Guid sprintId, CancellationToken cancellationToken = default)
        {
            var items = await retrospectiveRepository.FindAsync(sr => sr.SprintId == sprintId);
            var item = items.FirstOrDefault();
            if (item is null)
            {
                return Result.Failure<SprintRetrospectiveResponseDto>(CommonErrors.NotFound("SprintRetrospective"));
            }
            return Result.Success(MapToDto(item));
        }

        private static SprintRetrospectiveResponseDto MapToDto(SprintRetrospective sr) => new()
        {
            Id = sr.Id,
            SprintId = sr.SprintId,
            WhatWentWellEn = sr.WhatWentWellEn,
            WhatWentWellAr = sr.WhatWentWellAr,
            ChallengesEn = sr.ChallengesEn,
            ChallengesAr = sr.ChallengesAr,
            ActionItemsEn = sr.ActionItemsEn,
            ActionItemsAr = sr.ActionItemsAr,
            CompletionRate = sr.CompletionRate,
            EstimationAccuracy = sr.EstimationAccuracy,
            TeamSentimentSummaryEn = sr.TeamSentimentSummaryEn,
            TeamSentimentSummaryAr = sr.TeamSentimentSummaryAr
        };
    }
}
