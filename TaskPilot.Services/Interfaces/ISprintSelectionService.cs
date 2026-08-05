using System;
using System.Collections.Generic;
using TaskPilot.DTOs.Planning;
using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Interfaces
{
    public interface ISprintSelectionService
    {
        SprintSelectionResult SelectStories(
            List<UserStory> unassignedBacklog,
            List<Guid> partiallyCompletedCarryOverStoryIds,
            SprintSelectionOptions options);
    }
}
