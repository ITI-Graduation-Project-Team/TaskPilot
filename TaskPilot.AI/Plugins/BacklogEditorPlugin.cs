using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using TaskPilot.DTOs.Backlog;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Enums;

namespace TaskPilot.AI.Plugins
{
    public class BacklogEditorPlugin
    {
        private readonly IAiBacklogService _backlogService;
        private readonly ILogger<BacklogEditorPlugin> _logger;

        public BacklogEditorPlugin(IAiBacklogService backlogService, ILogger<BacklogEditorPlugin> logger)
        {
            _backlogService = backlogService;
            _logger = logger;
        }

        [KernelFunction("create_user_story")]
        [Description("Creates a new user story and its tasks. Provide ProjectId, Title, Description, Acceptance Criteria, Priority, and optionally a JSON array of tasks. If no tasks are provided, sensible defaults will be generated.")]
        public async Task<string> CreateUserStoryAsync(
            Kernel kernel,
            [Description("The unique identifier of the project")] Guid projectId,
            [Description("The title of the user story in English")] string titleEn,
            [Description("The description of the user story in English")] string descriptionEn,
            [Description("The acceptance criteria of the user story in English")] string acceptanceCriteriaEn,
            [Description("The title of the user story in Arabic")] string? titleAr,
            [Description("The description of the user story in Arabic")] string? descriptionAr,
            [Description("The acceptance criteria of the user story in Arabic")] string? acceptanceCriteriaAr,
            [Description("The priority of the user story (High, Medium, Low)")] string priority,
            [Description("Optional JSON array of tasks. Each task: {\"title\":\"...\",\"description\":\"...\",\"effortSize\":\"Small|Medium|Large\",\"type\":\"Technical|NonTechnical\",\"priority\":\"High|Medium|Low\",\"estimatedHours\":4}. Pass empty string if none.")] string? tasksJson = null)
        {
            if (!Enum.TryParse<StoryPriority>(priority, true, out var parsedPriority))
                parsedPriority = StoryPriority.Medium;

            // FIX 2: Duplicate guard — check existing stories before creating
            var existingBacklog = await _backlogService.GetBacklogAsync(projectId);
            if (existingBacklog.IsSuccess && existingBacklog.Value?.UserStories != null)
            {
                var normalizedNew = titleEn.Trim().ToLowerInvariant();
                var duplicate = existingBacklog.Value.UserStories.FirstOrDefault(s =>
                    s.Title != null &&
                    (s.Title.Trim().ToLowerInvariant().Contains(normalizedNew) ||
                     normalizedNew.Contains(s.Title.Trim().ToLowerInvariant())));

                if (duplicate != null)
                    return $"SKIPPED: A story with a similar title already exists: '{duplicate.Title}'. No duplicate was created.";
            }

            var translation = await TranslateToArabicAsync(kernel, titleEn, descriptionEn, acceptanceCriteriaEn);

            var request = new CreateUserStoryDto
            {
                TitleEn = titleEn,
                TitleAr = !string.IsNullOrWhiteSpace(titleAr) ? titleAr : (translation.TitleAr ?? titleEn),
                DescriptionEn = descriptionEn,
                DescriptionAr = !string.IsNullOrWhiteSpace(descriptionAr) ? descriptionAr : (translation.DescriptionAr ?? descriptionEn),
                AcceptanceCriteriaEn = acceptanceCriteriaEn,
                AcceptanceCriteriaAr = !string.IsNullOrWhiteSpace(acceptanceCriteriaAr) ? acceptanceCriteriaAr : (translation.AcceptanceCriteriaAr ?? acceptanceCriteriaEn),
                Priority = parsedPriority
            };

            var result = await _backlogService.CreateUserStoryAsync(projectId, request);
            if (result.IsSuccess)
            {
                var tasks = ParseOrGenerateDefaultTasks(titleEn, descriptionEn, tasksJson);
                foreach (var taskDto in tasks)
                {
                    if (string.IsNullOrWhiteSpace(taskDto.TitleAr) || string.IsNullOrWhiteSpace(taskDto.DescriptionAr))
                    {
                        var taskTrans = await TranslateToArabicAsync(kernel, taskDto.TitleEn, taskDto.DescriptionEn, null);
                        taskDto.TitleAr = !string.IsNullOrWhiteSpace(taskDto.TitleAr) ? taskDto.TitleAr : (taskTrans.TitleAr ?? taskDto.TitleEn);
                        taskDto.DescriptionAr = !string.IsNullOrWhiteSpace(taskDto.DescriptionAr) ? taskDto.DescriptionAr : (taskTrans.DescriptionAr ?? taskDto.DescriptionEn);
                    }
                    await _backlogService.CreateTaskAsync(result.Value!.Id, taskDto);
                }
                return $"Successfully created user story with {tasks.Count} tasks. Story ID: {result.Value!.Id}";
            }
            return $"Failed to create user story: {result.Error.Description}";
        }

        private class TranslationResult
        {
            public string? TitleAr { get; set; }
            public string? DescriptionAr { get; set; }
            public string? AcceptanceCriteriaAr { get; set; }
        }

        private async Task<TranslationResult> TranslateToArabicAsync(Kernel kernel, string titleEn, string? descriptionEn, string? acceptanceCriteriaEn)
        {
            try
            {
                var prompt = @"Translate the following English project management text to professional Arabic.
Return ONLY a valid JSON object with the exact keys: 'TitleAr', 'DescriptionAr', 'AcceptanceCriteriaAr'.
Do not include markdown blocks or any other text.

Title: {{$titleEn}}
Description: {{$descriptionEn}}
Acceptance Criteria: {{$acceptanceCriteriaEn}}";

                var arguments = TaskPilot.AI.Helpers.KernelArgumentsFactory.CreateDeterministicArguments();
                arguments["titleEn"] = titleEn;
                arguments["descriptionEn"] = descriptionEn ?? string.Empty;
                arguments["acceptanceCriteriaEn"] = acceptanceCriteriaEn ?? string.Empty;

                var result = await kernel.InvokePromptAsync(prompt, arguments);
                var json = result.GetValue<string>()?.Trim();
                
                if (!string.IsNullOrWhiteSpace(json))
                {
                    if (json.StartsWith("```json")) json = json.Substring(7, json.Length - 10).Trim();
                    else if (json.StartsWith("```")) json = json.Substring(3, json.Length - 6).Trim();

                    return JsonSerializer.Deserialize<TranslationResult>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TranslationResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to translate Arabic fields for story/task: {Title}", titleEn);
            }
            return new TranslationResult();
        }

        private List<CreateTaskDto> ParseOrGenerateDefaultTasks(string storyTitle, string? storyDescription, string? tasksJson)
        {
            if (!string.IsNullOrWhiteSpace(tasksJson))
            {
                try
                {
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    };
                    var parsed = JsonSerializer.Deserialize<List<PluginTaskInput>>(tasksJson, options);
                    if (parsed != null && parsed.Any())
                    {
                        return parsed.Select(p => new CreateTaskDto
                        {
                            TitleEn = p.Title ?? "New Task",
                            DescriptionEn = p.Description,
                            EffortSize = Enum.TryParse<EffortSize>(p.EffortSize, true, out var es) ? es : EffortSize.Medium,
                            Type = Enum.TryParse<TaskType>(p.Type, true, out var tt) ? tt : TaskType.Technical,
                            Priority = Enum.TryParse<TaskPriority>(p.Priority, true, out var tp) ? tp : TaskPriority.Medium,
                            EstimatedHours = p.EstimatedHours ?? 4m
                        }).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to parse tasksJson for story '{Title}'. Using default tasks.", storyTitle);
                }
            }

            return GenerateDefaultTasks(storyTitle, storyDescription);
        }

        private List<CreateTaskDto> GenerateDefaultTasks(string storyTitle, string? storyDescription)
        {
            var shortTitle = storyTitle.Length > 40 
                ? storyTitle[..40] + "..." 
                : storyTitle;

            var desc = storyDescription ?? storyTitle;

            return new List<CreateTaskDto>
            {
                new() {
                    TitleEn = $"Design & plan: {shortTitle}",
                    DescriptionEn = $"Define technical approach and acceptance criteria for: {desc}",
                    EstimatedHours = 2m,
                    EffortSize = EffortSize.Small,
                    Type = TaskType.Technical,
                    Priority = TaskPriority.High
                },
                new() {
                    TitleEn = $"Implement: {shortTitle}",
                    DescriptionEn = $"Develop the core functionality for: {desc}",
                    EstimatedHours = 8m,
                    EffortSize = EffortSize.Medium,
                    Type = TaskType.Technical,
                    Priority = TaskPriority.High
                },
                new() {
                    TitleEn = $"Test & review: {shortTitle}",
                    DescriptionEn = $"Write tests and conduct code review for: {desc}",
                    EstimatedHours = 4m,
                    EffortSize = EffortSize.Small,
                    Type = TaskType.Technical,
                    Priority = TaskPriority.Medium
                }
            };
        }

        private class PluginTaskInput
        {
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? EffortSize { get; set; }
            public string? Type { get; set; }
            public string? Priority { get; set; }
            public decimal? EstimatedHours { get; set; }
        }

        [KernelFunction("add_task_to_story")]
        [Description("Adds a new task to an existing user story. Provide the StoryId, task title, description, effort size, type, priority, and estimated hours.")]
        public async Task<string> AddTaskToStoryAsync(
            Kernel kernel,
            [Description("The unique identifier of the user story")] Guid storyId,
            [Description("The title of the task in English")] string titleEn,
            [Description("The title of the task in Arabic")] string? titleAr,
            [Description("The description of the task in English")] string? descriptionEn,
            [Description("The description of the task in Arabic")] string? descriptionAr,
            [Description("Effort size: Small, Medium, Large")] string effortSize,
            [Description("Type: Technical or NonTechnical")] string type,
            [Description("Priority: High, Medium, Low")] string priority,
            [Description("Estimated hours to complete the task")] decimal estimatedHours)
        {
            TranslationResult? translation = null;
            if (string.IsNullOrWhiteSpace(titleAr) || string.IsNullOrWhiteSpace(descriptionAr))
            {
                translation = await TranslateToArabicAsync(kernel, titleEn, descriptionEn, null);
            }

            var taskDto = new CreateTaskDto
            {
                TitleEn = titleEn,
                TitleAr = !string.IsNullOrWhiteSpace(titleAr) ? titleAr : (translation?.TitleAr ?? titleEn),
                DescriptionEn = descriptionEn,
                DescriptionAr = !string.IsNullOrWhiteSpace(descriptionAr) ? descriptionAr : (translation?.DescriptionAr ?? descriptionEn),
                EffortSize = Enum.TryParse<EffortSize>(effortSize, true, out var es) ? es : EffortSize.Medium,
                Type = Enum.TryParse<TaskType>(type, true, out var tt) ? tt : TaskType.Technical,
                Priority = Enum.TryParse<TaskPriority>(priority, true, out var tp) ? tp : TaskPriority.Medium,
                EstimatedHours = estimatedHours
            };

            var result = await _backlogService.CreateTaskAsync(storyId, taskDto);
            if (result.IsSuccess)
            {
                return $"Successfully added task '{titleEn}' to the story. Task ID: {result.Value!.Id}";
            }
            return $"Failed to add task: {result.Error.Description}";
        }

        [KernelFunction("update_task")]
        [Description("Updates an existing task's fields. Provide the ProjectId, TaskId, and ONLY the fields you want to change. Leave others null.")]
        public async Task<string> UpdateTaskAsync(
            [Description("The unique identifier of the project")] Guid projectId,
            [Description("The unique identifier of the task")] Guid taskId,
            [Description("The new title of the task in English")] string? titleEn = null,
            [Description("The new description of the task in English")] string? descriptionEn = null,
            [Description("New effort size: Small, Medium, Large")] string? effortSize = null,
            [Description("New type: Technical or NonTechnical")] string? type = null,
            [Description("New priority: High, Medium, Low")] string? priority = null,
            [Description("New estimated hours to complete the task")] decimal? estimatedHours = null)
        {
            var backlogResult = await _backlogService.GetBacklogAsync(projectId);
            if (!backlogResult.IsSuccess || backlogResult.Value?.UserStories == null)
            {
                return "Failed to retrieve the current backlog to perform the update.";
            }

            var existingTask = backlogResult.Value.UserStories
                .SelectMany(s => s.Tasks)
                .FirstOrDefault(t => t.Id == taskId);

            if (existingTask == null)
            {
                return $"Failed to update task: Task with ID {taskId} was not found in the current backlog.";
            }

            var request = new UpdateTaskDto
            {
                TitleEn = titleEn ?? existingTask.Title,
                TitleAr = null,
                DescriptionEn = descriptionEn ?? existingTask.Description,
                DescriptionAr = null,
                TechnicalSummaryEn = existingTask.TechnicalSummary,
                TechnicalSummaryAr = null,
                AcceptanceCriteriaEn = existingTask.AcceptanceCriteria,
                AcceptanceCriteriaAr = null,
                Priority = priority != null && Enum.TryParse<TaskPriority>(priority, true, out var tp) ? tp : Enum.Parse<TaskPriority>(existingTask.Priority),
                EstimatedHours = estimatedHours ?? existingTask.EstimatedHours,
                EffortSize = effortSize != null && Enum.TryParse<EffortSize>(effortSize, true, out var es) ? es : Enum.Parse<EffortSize>(existingTask.EffortSize),
                Type = type != null && Enum.TryParse<TaskType>(type, true, out var tt) ? tt : Enum.Parse<TaskType>(existingTask.Type),
                Status = Enum.Parse<TaskItemStatus>(existingTask.Status)
            };

            var result = await _backlogService.UpdateTaskAsync(taskId, request);
            if (result.IsSuccess)
            {
                return $"Successfully updated task '{request.TitleEn}'.";
            }
            return $"Failed to update task: {result.Error.Description}";
        }

        [KernelFunction("delete_task")]
        [Description("Deletes a specific task from a user story by its task ID.")]
        public async Task<string> DeleteTaskAsync(
            [Description("The unique identifier of the task to delete")] Guid taskId)
        {
            var result = await _backlogService.DeleteTaskAsync(taskId);
            if (result.IsSuccess)
            {
                return "Successfully deleted task.";
            }
            return $"Failed to delete task: {result.Error.Description}";
        }

        [KernelFunction("update_user_story")]
        [Description("Updates an existing user story. Provide the StoryId, new Title, Description, Acceptance Criteria, and Priority.")]
        public async Task<string> UpdateUserStoryAsync(
            [Description("The unique identifier of the user story to update")] Guid storyId,
            [Description("The new title of the user story in English")] string titleEn,
            [Description("The new description of the user story in English")] string descriptionEn,
            [Description("The new acceptance criteria of the user story in English")] string acceptanceCriteriaEn,
            [Description("The new priority of the user story (High, Medium, Low)")] string priority)
        {
            if (!Enum.TryParse<StoryPriority>(priority, true, out var parsedPriority))
                parsedPriority = StoryPriority.Medium;

            var request = new UpdateUserStoryDto
            {
                TitleEn = titleEn,
                DescriptionEn = descriptionEn,
                AcceptanceCriteriaEn = acceptanceCriteriaEn,
                Priority = parsedPriority
            };

            var result = await _backlogService.UpdateUserStoryAsync(storyId, request);
            if (result.IsSuccess)
            {
                return "Successfully updated user story.";
            }
            return $"Failed to update user story: {result.Error.Description}";
        }

        [KernelFunction("delete_user_story")]
        [Description("Deletes a user story by its ID.")]
        public async Task<string> DeleteUserStoryAsync(
            [Description("The unique identifier of the user story to delete")] Guid storyId)
        {
            var result = await _backlogService.DeleteUserStoryAsync(storyId);
            if (result.IsSuccess)
            {
                return "Successfully deleted user story.";
            }
            return $"Failed to delete user story: {result.Error.Description}";
        }
    }
}
