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
        private readonly ICapacityCalculationService _capacityCalculationService;
        private readonly ISprintSelectionService _sprintSelectionService;
        private readonly ILogger<SprintPlanningService> _logger;

        public SprintPlanningService(
            IRepository<Project> projectRepository,
            IUserStoryRepository userStoryRepository,
            IProjectEmployeeRepository projectEmployeeRepository,
            IRepository<SprintRetrospective> retrospectiveRepository,
            IRepository<Sprint> sprintRepository,
            SprintDataCollectionService dataCollectionService,
            SprintSuggestionAgent sprintSuggestionAgent,
            ICapacityCalculationService capacityCalculationService,
            ISprintSelectionService sprintSelectionService,
            ILogger<SprintPlanningService> logger)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _retrospectiveRepository = retrospectiveRepository;
            _sprintRepository = sprintRepository;
            _dataCollectionService = dataCollectionService;
            _sprintSuggestionAgent = sprintSuggestionAgent;
            _capacityCalculationService = capacityCalculationService;
            _sprintSelectionService = sprintSelectionService;
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

                var hasPlannedSprint = await _sprintRepository.GetQueryable()
                    .AnyAsync(s => s.ProjectId == projectId && s.Status == SprintStatus.Planned, cancellationToken);
                if (hasPlannedSprint)
                {
                    return Result.Failure<SprintSuggestionDto>(SprintErrors.AnotherSprintAlreadyPlanned);
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

            // Fetch previous retrospective context
            var previousRetrospective = await _retrospectiveRepository.GetQueryable()
                .Include(r => r.Sprint)
                .Where(r => r.Sprint.ProjectId == projectId)
                .OrderByDescending(r => r.Sprint.EndDate)
                .FirstOrDefaultAsync(cancellationToken);

            var retrospectiveContext = string.Empty;
            var carryOverStoryIds = new HashSet<Guid>();

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
                            if (story.UserStoryId != Guid.Empty) carryOverStoryIds.Add(story.UserStoryId);
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
                                if (task.UserStoryId.HasValue && task.UserStoryId.Value != Guid.Empty)
                                {
                                    carryOverStoryIds.Add(task.UserStoryId.Value);
                                }
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

            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(project.SprintDurationInDays - 1);
            
            var capacityResult = await _capacityCalculationService.CalculateTargetSprintHoursAsync(projectId, startDate, endDate, cancellationToken);
            if (!capacityResult.IsSuccess)
            {
                return Result.Failure<SprintSuggestionDto>(capacityResult.Error!);
            }

            // 1. Run Deterministic Selection Algorithm
            var options = new SprintSelectionOptions { TargetSprintHours = capacityResult.Value!.TargetSprintHours };
            var selectionResult = _sprintSelectionService.SelectStories(unassignedStories, carryOverStoryIds.ToList(), options);

            // 2. Map AI Payload (Selected Stories & Excluded Stories Summary)
            var selectedStoriesJson = JsonSerializer.Serialize(selectionResult.SelectedStories, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            
            var highlightedExcluded = new List<object>();
            var summaryCounts = new Dictionary<string, int>();

            foreach (var excluded in selectionResult.ExcludedStories)
            {
                var originalStory = unassignedStories.FirstOrDefault(u => u.Id == excluded.StoryId);
                bool isHighlighted = originalStory != null && (carryOverStoryIds.Contains(originalStory.Id) || originalStory.Priority == StoryPriority.Critical || originalStory.Priority == StoryPriority.High);
                
                if (isHighlighted)
                {
                    highlightedExcluded.Add(new
                    {
                        excluded.StoryId,
                        excluded.TitleEn,
                        excluded.Reason
                    });
                }
                else
                {
                    if (!summaryCounts.ContainsKey(excluded.Reason))
                        summaryCounts[excluded.Reason] = 0;
                    summaryCounts[excluded.Reason]++;
                }
            }

            var summaryText = summaryCounts.Any() 
                ? $"{summaryCounts.Values.Sum()} additional lower-priority stories excluded: " + string.Join(", ", summaryCounts.Select(kvp => $"{kvp.Value} due to '{kvp.Key}'"))
                : "No additional stories excluded.";

            var excludedPayload = new
            {
                highlighted = highlightedExcluded,
                summary = summaryText
            };

            var excludedStoriesJson = JsonSerializer.Serialize(excludedPayload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });

            // 3. Call AI Agent
            SprintSuggestionDto suggestion;
            try
            {
                suggestion = await _sprintSuggestionAgent.SuggestSprintAsync(
                    projectId,
                    project.NameEn,
                    project.SprintDurationInDays,
                    capacityResult.Value!.TargetSprintHours,
                    selectionResult.UtilizedHours,
                    selectedStoriesJson,
                    excludedStoriesJson,
                    retrospectiveContext,
                    nextSprintNumber,
                    cancellationToken);
                    
                // Append transparency fields from C# calculation
                suggestion.CapacityExplanationEn = capacityResult.Value!.ExplanationEn;
                suggestion.CapacityExplanationAr = capacityResult.Value!.ExplanationAr;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sprint generation failed.");
                return Result.Failure<SprintSuggestionDto>(CommonErrors.InvalidInput("Failed to generate sprint suggestion due to an internal AI error."));
            }

            // 4. Merge AI Output with Deterministic Selection Data
            // The algorithm is authoritative on: StoryId, EstimatedHours, TotalEstimatedHours, ExcludedStories
            // The AI is authoritative on: SprintTitle, SprintGoal, Risks, and Story Rationale (ReasonEn/Ar)

            suggestion.TotalEstimatedHours = selectionResult.UtilizedHours;
            suggestion.ExcludedStories = selectionResult.ExcludedStories;

            // Map rationale to the selected stories
            var finalStories = new List<SuggestedStoryDto>();
            foreach (var algoStory in selectionResult.SelectedStories)
            {
                var aiStory = suggestion.Stories.FirstOrDefault(s => s.StoryId == algoStory.StoryId);
                algoStory.ReasonEn = aiStory?.ReasonEn ?? string.Empty;
                algoStory.ReasonAr = aiStory?.ReasonAr ?? string.Empty;
                
                finalStories.Add(algoStory);
            }

            suggestion.Stories = finalStories;

            return Result.Success(suggestion);
        }
    }
}
