using System;
using System.Collections.Generic;
using System.Linq;
using TaskPilot.DTOs.Planning;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class SprintSelectionService : ISprintSelectionService
    {
        public SprintSelectionResult SelectStories(
            List<UserStory> unassignedBacklog,
            List<Guid> partiallyCompletedCarryOverStoryIds,
            SprintSelectionOptions options)
        {
            var result = new SprintSelectionResult
            {
                TargetHours = options.TargetSprintHours
            };

            if (unassignedBacklog == null || !unassignedBacklog.Any())
            {
                return result;
            }

            // 1. Dependency Cycle Detection
            var cycleExcludedIds = DetectAndExcludeCycles(unassignedBacklog, result.ExcludedStories);

            // 2. Sort Eligible Backlog
            // CarryOver > Critical > High > Medium > Low
            // Tie-break: Smallest-Hours-First
            var sortedBacklog = unassignedBacklog
                .Where(us => !cycleExcludedIds.Contains(us.Id))
                .Select(us => new
                {
                    Story = us,
                    TotalEstimatedHours = us.Tasks.Where(t => t.Status != TaskItemStatus.Done).Sum(t => t.EstimatedHours),
                    IsCarryOver = partiallyCompletedCarryOverStoryIds.Contains(us.Id)
                })
                .OrderByDescending(x => x.IsCarryOver)
                .ThenByDescending(x => GetPriorityValue(x.Story.Priority))
                .ThenBy(x => x.TotalEstimatedHours)
                .ToList();

            var selectedIds = new HashSet<Guid>();
            var unassignedIds = new HashSet<Guid>(unassignedBacklog.Select(u => u.Id));

            // 3. Selection Loop (Greedy with Dependency Enforcement and Overflow Scanning)
            foreach (var item in sortedBacklog)
            {
                var story = item.Story;

                // Check Dependency Constraint
                if (story.DependsOnStoryId.HasValue)
                {
                    var depId = story.DependsOnStoryId.Value;
                    
                    // If dependency is in unassigned backlog but NOT selected in this sprint yet, we can't select this story.
                    if (unassignedIds.Contains(depId) && !selectedIds.Contains(depId))
                    {
                        var depStory = unassignedBacklog.FirstOrDefault(u => u.Id == depId);
                        var depName = depStory?.TitleEn ?? depId.ToString();
                        
                        result.ExcludedStories.Add(new ExcludedStoryDto
                        {
                            StoryId = story.Id,
                            TitleEn = story.TitleEn,
                            Reason = $"Excluded due to unmet dependency on '{depName}'."
                        });
                        continue;
                    }
                }

                // Check Capacity Constraint
                decimal maxAllowedHours = options.TargetSprintHours * options.MaxUtilizationPercent;
                
                if (result.UtilizedHours + item.TotalEstimatedHours <= maxAllowedHours)
                {
                    // Select it
                    selectedIds.Add(story.Id);
                    result.UtilizedHours += item.TotalEstimatedHours;

                    result.SelectedStories.Add(new SuggestedStoryDto
                    {
                        StoryId = story.Id,
                        TitleEn = story.TitleEn,
                        TitleAr = story.TitleAr,
                        EstimatedHours = item.TotalEstimatedHours,
                        PriorityScore = (item.IsCarryOver ? 1000 : GetPriorityValue(story.Priority)), 
                        ReasonEn = string.Empty, 
                        ReasonAr = string.Empty
                    });
                }
                else
                {
                    // Oversized, skip but keep scanning
                    result.ExcludedStories.Add(new ExcludedStoryDto
                    {
                        StoryId = story.Id,
                        TitleEn = story.TitleEn,
                        Reason = "Excluded due to sprint capacity limits."
                    });
                }
            }

            return result;
        }

        private HashSet<Guid> DetectAndExcludeCycles(List<UserStory> backlog, List<ExcludedStoryDto> excludedStories)
        {
            var excludedIds = new HashSet<Guid>();
            var adjList = new Dictionary<Guid, Guid>();

            // Build simple adj list for dependencies in the unassigned backlog
            foreach (var story in backlog)
            {
                if (story.DependsOnStoryId.HasValue)
                {
                    adjList[story.Id] = story.DependsOnStoryId.Value;
                }
            }

            var visited = new HashSet<Guid>();
            var recursionStack = new HashSet<Guid>();

            foreach (var story in backlog)
            {
                if (!visited.Contains(story.Id))
                {
                    var cycleNodes = new List<Guid>();
                    if (HasCycle(story.Id, adjList, visited, recursionStack, cycleNodes))
                    {
                        // A cycle was found in this chain, exclude all involved nodes in the cycle
                        foreach (var cycleNodeId in cycleNodes)
                        {
                            if (excludedIds.Add(cycleNodeId))
                            {
                                var nodeStory = backlog.First(s => s.Id == cycleNodeId);
                                excludedStories.Add(new ExcludedStoryDto
                                {
                                    StoryId = cycleNodeId,
                                    TitleEn = nodeStory.TitleEn,
                                    Reason = "Excluded due to a circular dependency cycle."
                                });
                            }
                        }
                    }
                }
            }

            return excludedIds;
        }

        private bool HasCycle(Guid currentId, Dictionary<Guid, Guid> adjList, HashSet<Guid> visited, HashSet<Guid> recursionStack, List<Guid> cycleNodes)
        {
            visited.Add(currentId);
            recursionStack.Add(currentId);

            try
            {
                if (adjList.TryGetValue(currentId, out Guid depId))
                {
                    if (!visited.Contains(depId))
                    {
                        if (HasCycle(depId, adjList, visited, recursionStack, cycleNodes))
                        {
                            if (recursionStack.Contains(currentId))
                            {
                                cycleNodes.Add(currentId);
                            }
                            return true;
                        }
                    }
                    else if (recursionStack.Contains(depId))
                    {
                        // Found cycle!
                        cycleNodes.Add(depId);
                        cycleNodes.Add(currentId);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                recursionStack.Remove(currentId);
            }
        }

        private int GetPriorityValue(StoryPriority priority)
        {
            return priority switch
            {
                StoryPriority.Critical => 400,
                StoryPriority.High => 300,
                StoryPriority.Medium => 200,
                StoryPriority.Low => 100,
                _ => 0
            };
        }
    }
}
