using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Planning;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Implementations;

namespace TaskPilot.Services
{
    public class SprintPlanningService : ISprintPlanningService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IUserStoryRepository _userStoryRepository;
        private readonly IProjectEmployeeRepository _projectEmployeeRepository;
        private readonly IRepository<SprintRetrospective> _retrospectiveRepository;
        private readonly IRepository<Sprint> _sprintRepository;
        private readonly SprintDataCollectionService _dataCollectionService;
        private readonly SprintSuggestionAgent _sprintSuggestionAgent;
        private readonly ILogger<SprintPlanningService> _logger;

        public SprintPlanningService(
            IRepository<Project> projectRepository,
            IUserStoryRepository userStoryRepository,
            IProjectEmployeeRepository projectEmployeeRepository,
            IRepository<SprintRetrospective> retrospectiveRepository,
            IRepository<Sprint> sprintRepository,
            SprintDataCollectionService dataCollectionService,
            SprintSuggestionAgent sprintSuggestionAgent,
            ILogger<SprintPlanningService> logger)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _retrospectiveRepository = retrospectiveRepository;
            _sprintRepository = sprintRepository;
            _dataCollectionService = dataCollectionService;
            _sprintSuggestionAgent = sprintSuggestionAgent;
            _logger = logger;
        }

        public async Task<Result<SprintSuggestionDto>> GenerateSprintSuggestionAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<SprintSuggestionDto>(CommonErrors.NotFound("Project"));
            }

            if (_sprintRepository != null)
            {
                var hasActiveSprint = await _sprintRepository.GetQueryable()
                    .AnyAsync(s => s.ProjectId == projectId && s.Status == SprintStatus.Active, cancellationToken);
                if (hasActiveSprint)
                {
                    return Result.Failure<SprintSuggestionDto>(SprintErrors.AnotherSprintAlreadyActive);
                }
            }

            var assignedEmployees = await _projectEmployeeRepository.GetEmployeeIdsByProjectAsync(projectId, cancellationToken);
            if (!assignedEmployees.Any())
            {
                return Result.Failure<SprintSuggestionDto>(SprintErrors.NoEmployeesAssigned);
            }

            var userStories = await _userStoryRepository.GetByProjectIdAsync(projectId, cancellationToken);
            if (!userStories.Any())
            {
                return Result.Failure<SprintSuggestionDto>(CommonErrors.InvalidInput("Project contains no backlog."));
            }

            var unassignedStories = userStories.Where(us => us.SprintId == null).ToList();

            if (!unassignedStories.Any())
            {
                return Result.Failure<SprintSuggestionDto>(CommonErrors.InvalidInput("Every UserStory already belongs to a Sprint."));
            }

            var backlogData = unassignedStories.Select(us => new
            {
                us.Id,
                us.TitleEn,
                us.TitleAr,
                us.DescriptionEn,
                us.DescriptionAr,
                us.AcceptanceCriteriaEn,
                us.AcceptanceCriteriaAr,
                Priority = us.Priority.ToString(),
                Tasks = us.Tasks.Where(t => t.Status != TaskItemStatus.Done).Select(t => new
                {
                    t.TitleEn,
                    t.TitleAr,
                    t.EstimatedHours,
                    EffortSize = t.EffortSize.ToString(),
                    Priority = t.Priority.ToString(),
                    Type = t.Type.ToString()
                }).ToList(),
                TotalEstimatedHours = us.Tasks.Where(t => t.Status != TaskItemStatus.Done).Sum(t => t.EstimatedHours)
            }).ToList();

            var backlogJson = JsonSerializer.Serialize(backlogData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            // Fetch previous retrospective context
            var previousRetrospective = await _retrospectiveRepository.GetQueryable()
                .Include(r => r.Sprint)
                .Where(r => r.Sprint.ProjectId == projectId)
                .OrderByDescending(r => r.Sprint.EndDate)
                .FirstOrDefaultAsync(cancellationToken);

            var retrospectiveContext = string.Empty;

            if (previousRetrospective is not null)
            {
                var contextLines = new List<string>();

                // 1. High Priority Improvements
                var improvements = string.IsNullOrEmpty(previousRetrospective.ImprovementsJson) 
                    ? new List<SprintImprovementDto>() 
                    : JsonSerializer.Deserialize<List<SprintImprovementDto>>(previousRetrospective.ImprovementsJson);

                if (improvements != null && improvements.Any(i => i.Priority == "High"))
                {
                    contextLines.Add("Previous Sprint Actionable Improvements:");
                    foreach (var imp in improvements.Where(i => i.Priority == "High"))
                    {
                        contextLines.Add($"  - [{imp.ActionType}] {imp.RecommendationEn}");
                    }
                }

                // 2. Fetch Detailed Retrospective Metrics & Carry-over Data
                try
                {
                    var retroData = await _dataCollectionService.CollectAsync(previousRetrospective.SprintId, cancellationToken);

                    // Feature Completeness & Hierarchical Carry-over Tasks (Ideas 1 & 5)
                    if (retroData.PartiallyCompletedStories.Any() || retroData.UnfinishedTasks.Any())
                    {
                        contextLines.Add("PARTIALLY COMPLETED FEATURES & CARRY-OVER WORK FROM PREVIOUS SPRINT (Close these first for 100% Shippable Value):");

                        var attachedTaskIds = new HashSet<Guid>();

                        foreach (var story in retroData.PartiallyCompletedStories)
                        {
                            contextLines.Add($"  - Story '{story.TitleEn}' (ID: {story.UserStoryId}) is {story.CompletionPercentage}% complete ({story.CompletedTasks}/{story.TotalTasks} tasks done):");

                            var storyTasks = retroData.UnfinishedTasks.Where(t => t.UserStoryId == story.UserStoryId).ToList();
                            foreach (var task in storyTasks)
                            {
                                attachedTaskIds.Add(task.TaskId);
                                contextLines.Add($"      └── Remaining Task: '{task.TitleEn}' (Estimated: {task.EstimatedHours}h, Status: {task.Reason}, Assignee: {task.AssigneeName})");
                            }
                        }

                        var orphanTasks = retroData.UnfinishedTasks.Where(t => !attachedTaskIds.Contains(t.TaskId)).ToList();
                        if (orphanTasks.Any())
                        {
                            contextLines.Add("  - Unattached Carry-Over Tasks:");
                            foreach (var task in orphanTasks)
                            {
                                contextLines.Add($"      └── Task: '{task.TitleEn}' (Estimated: {task.EstimatedHours}h, Status: {task.Reason}, Assignee: {task.AssigneeName})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not collect detailed retrospective metrics for SprintId {SprintId}", previousRetrospective.SprintId);
                }

                if (contextLines.Any())
                {
                    retrospectiveContext = string.Join("\n", contextLines);
                }
            }

            _logger.LogInformation("Generating Sprint suggestion for ProjectId: {ProjectId} with {StoryCount} unassigned stories", projectId, unassignedStories.Count);

            var existingSprintsCount = _sprintRepository != null
                ? await _sprintRepository.GetQueryable().CountAsync(s => s.ProjectId == projectId, cancellationToken)
                : 0;

            var nextSprintNumber = existingSprintsCount + 1;

            var suggestion = await _sprintSuggestionAgent.SuggestSprintAsync(
                projectId,
                project.NameEn,
                project.SprintDurationInDays,
                project.TargetSprintHours,
                backlogJson,
                retrospectiveContext,
                nextSprintNumber,
                cancellationToken);

            return Result.Success(suggestion);
        }
    }
}
