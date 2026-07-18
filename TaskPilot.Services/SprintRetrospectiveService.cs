using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SprintRetrospectiveService(
        IRepository<Sprint> sprintRepository,
        IRepository<SprintRetrospective> retrospectiveRepository,
        IRepository<SprintRiskAlert> sprintRiskAlertRepository,
        SprintRetrospectiveAgent retrospectiveAgent) : ISprintRetrospectiveService
    {
        public async Task<Result<SprintRetrospectiveResponseDto>> GenerateRetrospectiveAsync(Guid sprintId, CancellationToken cancellationToken = default)
        {
            var sprint = await sprintRepository.GetQueryable()
                .Include(s => s.Project)
                .Include(s => s.Tasks)
                    .ThenInclude(t => t.Comments)
                .FirstOrDefaultAsync(s => s.Id == sprintId, cancellationToken);

            if (sprint is null)
            {
                return Result.Failure<SprintRetrospectiveResponseDto>(CommonErrors.NotFound("Sprint"));
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

            // Fetch delayed tasks from SprintRiskAlerts because they are dissociated from the sprint
            var riskAlerts = await sprintRiskAlertRepository.GetQueryable()
                .Include(a => a.AffectedTask)
                    .ThenInclude(t => t!.Comments)
                .Where(a => a.SprintId == sprintId && a.RiskType == SprintRiskType.UnfinishedTask)
                .ToListAsync(cancellationToken);

            var completedTasks = sprint.Tasks.Where(t => t.Status == TaskItemStatus.Done).ToList();

            var completedTasksData = completedTasks.Select(t => new
            {
                t.TitleEn,
                Estimated = t.EstimatedHours,
                Actual = t.ActualHours
            }).ToList();

            var delayedTasksData = riskAlerts.Select(a => new
            {
                TitleEn = a.AffectedTask?.TitleEn ?? string.Empty,
                Status = a.AffectedTask?.Status.ToString() ?? TaskItemStatus.ToDo.ToString(),
                Estimated = a.AffectedTask?.EstimatedHours ?? 0
            }).ToList();

            var commentsList = new List<object>();

            // Comments from completed tasks
            foreach (var task in completedTasks)
            {
                foreach (var comment in task.Comments)
                {
                    string role = "Team Member";
                    if (comment.UserId == sprint.Project.ManagerId)
                    {
                        role = "Project Manager";
                    }
                    else if (comment.UserId == task.EmployeeId)
                    {
                        role = "Assigned Employee";
                    }

                    commentsList.Add(new
                    {
                        TaskTitle = task.TitleEn,
                        AuthorRole = role,
                        Content = comment.Content
                    });
                }
            }

            // Comments from unfinished tasks
            foreach (var alert in riskAlerts)
            {
                if (alert.AffectedTask != null)
                {
                    foreach (var comment in alert.AffectedTask.Comments)
                    {
                        string role = "Team Member";
                        if (comment.UserId == sprint.Project.ManagerId)
                        {
                            role = "Project Manager";
                        }
                        else if (comment.UserId == alert.AffectedEmployeeId)
                        {
                            role = "Assigned Employee";
                        }

                        commentsList.Add(new
                        {
                            TaskTitle = alert.AffectedTask.TitleEn,
                            AuthorRole = role,
                            Content = comment.Content
                        });
                    }
                }
            }

            decimal expectedHours = completedTasks.Sum(t => t.EstimatedHours) + riskAlerts.Sum(a => a.AffectedTask?.EstimatedHours ?? 0);
            decimal actualHours = completedTasks.Sum(t => t.ActualHours);

            var aiResult = await retrospectiveAgent.AnalyzeSprintAsync(
                sprint.SprintGoalEn ?? string.Empty,
                JsonSerializer.Serialize(completedTasksData),
                JsonSerializer.Serialize(delayedTasksData),
                JsonSerializer.Serialize(commentsList),
                cancellationToken);

            double completionRate = (completedTasks.Count + riskAlerts.Count) > 0
                ? (double)completedTasks.Count / (completedTasks.Count + riskAlerts.Count) * 100
                : 0.0;

            decimal accuracy = actualHours > 0 ? (expectedHours / actualHours) * 100 : 100;

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
            ExpectedHours = sr.ExpectedHours,
            ActualHours = sr.ActualHours,
            TeamSentimentSummaryEn = sr.TeamSentimentSummaryEn,
            TeamSentimentSummaryAr = sr.TeamSentimentSummaryAr
        };
    }
}
