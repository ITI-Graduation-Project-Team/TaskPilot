using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Planning;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Services
{
    public class SprintPlanningService : ISprintPlanningService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IUserStoryRepository _userStoryRepository;
        private readonly SprintSuggestionAgent _sprintSuggestionAgent;
        private readonly ILogger<SprintPlanningService> _logger;

        public SprintPlanningService(
            IRepository<Project> projectRepository,
            IUserStoryRepository userStoryRepository,
            SprintSuggestionAgent sprintSuggestionAgent,
            ILogger<SprintPlanningService> logger)
        {
            _projectRepository = projectRepository;
            _userStoryRepository = userStoryRepository;
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
                Tasks = us.Tasks.Select(t => new
                {
                    t.TitleEn,
                    t.TitleAr,
                    t.EstimatedHours,
                    EffortSize = t.EffortSize.ToString(),
                    Priority = t.Priority.ToString(),
                    Type = t.Type.ToString()
                }).ToList(),
                TotalEstimatedHours = us.Tasks.Sum(t => t.EstimatedHours)
            }).ToList();

            var backlogJson = JsonSerializer.Serialize(backlogData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            _logger.LogInformation("Generating Sprint suggestion for ProjectId: {ProjectId} with {StoryCount} unassigned stories", projectId, unassignedStories.Count);

            var suggestion = await _sprintSuggestionAgent.SuggestSprintAsync(
                projectId,
                project.NameEn,
                project.SprintDurationInDays,
                project.TargetSprintHours,
                backlogJson,
                cancellationToken);

            return Result.Success(suggestion);
        }
    }
}
