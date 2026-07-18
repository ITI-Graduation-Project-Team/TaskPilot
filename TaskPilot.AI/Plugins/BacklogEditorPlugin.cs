using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.DTOs.Backlog;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Enums;

namespace TaskPilot.AI.Plugins
{
    public class BacklogEditorPlugin
    {
        private readonly IAiBacklogService _backlogService;

        public BacklogEditorPlugin(IAiBacklogService backlogService)
        {
            _backlogService = backlogService;
        }

        [KernelFunction("create_user_story")]
        [Description("Creates a new user story for the project. Provide the ProjectId, Title, Description, Acceptance Criteria, and Priority (High/Medium/Low).")]
        public async Task<string> CreateUserStoryAsync(
            [Description("The unique identifier of the project")] Guid projectId,
            [Description("The title of the user story in English")] string titleEn,
            [Description("The description of the user story in English")] string descriptionEn,
            [Description("The acceptance criteria of the user story in English")] string acceptanceCriteriaEn,
            [Description("The priority of the user story (High, Medium, Low)")] string priority)
        {
            if (!Enum.TryParse<StoryPriority>(priority, true, out var parsedPriority))
                parsedPriority = StoryPriority.Medium;

            // FIX 2: Duplicate guard — check existing stories before creating
            var existingBacklog = await _backlogService.GetBacklogAsync(projectId);
            if (existingBacklog.IsSuccess && existingBacklog.Value?.UserStories != null)
            {
                var normalizedNew = titleEn.Trim().ToLowerInvariant();
                var duplicate = existingBacklog.Value.UserStories.FirstOrDefault(s =>
                    s.TitleEn != null &&
                    (s.TitleEn.Trim().ToLowerInvariant().Contains(normalizedNew) ||
                     normalizedNew.Contains(s.TitleEn.Trim().ToLowerInvariant())));

                if (duplicate != null)
                    return $"SKIPPED: A story with a similar title already exists: '{duplicate.TitleEn}'. No duplicate was created.";
            }

            var request = new CreateUserStoryDto
            {
                TitleEn = titleEn,
                DescriptionEn = descriptionEn,
                AcceptanceCriteriaEn = acceptanceCriteriaEn,
                Priority = parsedPriority
            };

            var result = await _backlogService.CreateUserStoryAsync(projectId, request);
            if (result.IsSuccess)
            {
                return $"Successfully created user story. ID: {result.Value!.Id}";
            }
            return $"Failed to create user story: {result.Error.Description}";
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
