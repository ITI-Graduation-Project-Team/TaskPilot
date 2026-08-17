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

            // ── Fix 4: Pre-screen stories with no tasks attached (0h estimated).
            //    These enter the sprint "for free" and inflate SelectedStories.Count
            //    without consuming any capacity, which distorts scope reporting.
            //    Decision: exclude them immediately with an actionable reason so PMs
            //    are prompted to add tasks. (Product note: if there is ever a need to
            //    include task-less stories for tracking purposes, change this to Option B
            //    — defer them to end-of-list — rather than removing the guard entirely.)
            var noTasksExcludedIds = new HashSet<Guid>();
            foreach (var story in unassignedBacklog)
            {
                if (!story.Tasks.Any())
                {
                    noTasksExcludedIds.Add(story.Id);
                    result.ExcludedStories.Add(new ExcludedStoryDto
                    {
                        StoryId = story.Id,
                        TitleEn = story.TitleEn,
                        Reason = "Excluded: no tasks attached (0h estimated). Add tasks before sprint planning."
                    });
                }
            }

            // ── Step 1: Dependency Cycle Detection.
            //    Only considers stories not already excluded by the 0h guard.
            var eligibleForCycleCheck = unassignedBacklog
                .Where(us => !noTasksExcludedIds.Contains(us.Id))
                .ToList();

            var cycleExcludedIds = DetectAndExcludeCycles(eligibleForCycleCheck, result.ExcludedStories);

            // Combined set of IDs that must not enter topo-sort / greedy loop.
            var fullyExcludedIds = new HashSet<Guid>(noTasksExcludedIds);
            fullyExcludedIds.UnionWith(cycleExcludedIds);

            // ── Step 2: Build enriched metadata for each eligible story.
            var eligibleStories = unassignedBacklog
                .Where(us => !fullyExcludedIds.Contains(us.Id))
                .Select(us => new StoryItem(
                    story: us,
                    totalEstimatedHours: us.Tasks
                        .Where(t => t.Status != TaskItemStatus.Done)
                        .Sum(t => t.EstimatedHours),
                    isCarryOver: partiallyCompletedCarryOverStoryIds.Contains(us.Id)))
                .ToList();

            // Index for O(1) lookup by ID.
            var itemById = eligibleStories.ToDictionary(x => x.Story.Id);

            // ── Step 3 (Fix 1): Kahn's algorithm with a priority queue.
            //    Guarantees every prerequisite appears before its dependent in the
            //    iteration sequence, making a single greedy pass correct by construction.

            // Build in-degree map and reverse-adjacency (who depends on me?).
            var inDegree = new Dictionary<Guid, int>();
            var dependents = new Dictionary<Guid, List<Guid>>(); // depId → [stories that depend on it]

            foreach (var item in eligibleStories)
            {
                inDegree[item.Story.Id] = 0;
                dependents[item.Story.Id] = new List<Guid>();
            }

            foreach (var item in eligibleStories)
            {
                if (item.Story.DependsOnStoryId.HasValue)
                {
                    var depId = item.Story.DependsOnStoryId.Value;

                    if (itemById.ContainsKey(depId))
                    {
                        // Dependency is itself eligible → register the edge.
                        inDegree[item.Story.Id]++;
                        dependents[depId].Add(item.Story.Id);
                    }
                    // If depId is not in itemById it was excluded (cycle / 0h) — handled
                    // below in the dependency-on-excluded-node defensive check.
                }
            }

            // Priority queue comparator: CarryOver > Priority desc > EstHours asc.
            // Lower numeric value = higher queue priority (MinHeap behaviour).
            var pq = new SortedSet<StoryItem>(Comparer<StoryItem>.Create((a, b) =>
            {
                // 1. Carry-over wins.
                int cmp = b.IsCarryOver.CompareTo(a.IsCarryOver);
                if (cmp != 0) return cmp;

                // 2. Higher priority wins.
                cmp = GetPriorityValue(b.Story.Priority).CompareTo(GetPriorityValue(a.Story.Priority));
                if (cmp != 0) return cmp;

                // 3. Smaller hours wins (tie-break).
                cmp = a.TotalEstimatedHours.CompareTo(b.TotalEstimatedHours);
                if (cmp != 0) return cmp;

                // 4. Stable tie-break on ID so SortedSet never silently drops duplicates.
                return a.Story.Id.CompareTo(b.Story.Id);
            }));

            // Seed: all eligible stories with no unresolved dependencies.
            foreach (var item in eligibleStories)
            {
                if (inDegree[item.Story.Id] == 0)
                {
                    pq.Add(item);
                }
            }

            // ── Step 4: Greedy capacity-fill loop on topologically-ordered items.
            var selectedIds = new HashSet<Guid>();
            var unassignedIds = new HashSet<Guid>(unassignedBacklog.Select(u => u.Id));
            decimal maxAllowedHours = options.TargetSprintHours * options.MaxUtilizationPercent;

            while (pq.Count > 0)
            {
                // Dequeue highest-priority item with in-degree 0.
                var item = pq.Min!;
                pq.Remove(item);
                var story = item.Story;

                // ── Defensive check (Fix 1 requirement §5):
                //    If this story's dependency was excluded for another reason
                //    (cycle / 0h), we still must not select it — its prerequisite
                //    will never appear in the sprint.
                if (story.DependsOnStoryId.HasValue)
                {
                    var depId = story.DependsOnStoryId.Value;
                    bool depWasExcluded = fullyExcludedIds.Contains(depId);

                    // If depId was in-scope but ended up excluded by cycle/0h,
                    // this story is blocked.
                    if (depWasExcluded)
                    {
                        var depStory = unassignedBacklog.FirstOrDefault(u => u.Id == depId);
                        var depName = depStory?.TitleEn ?? depId.ToString();
                        result.ExcludedStories.Add(new ExcludedStoryDto
                        {
                            StoryId = story.Id,
                            TitleEn = story.TitleEn,
                            Reason = $"Excluded: prerequisite '{depName}' was itself excluded (cycle or no tasks)."
                        });

                        // Propagate: unblock dependents that were waiting on this
                        // now-excluded story so they can be evaluated and excluded too.
                        PropagateExclusion(story.Id, dependents, inDegree, itemById, pq, fullyExcludedIds);
                        continue;
                    }

                    // If dependency is outside the unassigned backlog entirely
                    // (already in a previous sprint → considered "done"), allow selection.
                    // No action needed here.
                }

                // ── Capacity check (unchanged logic, just fed correct order now).
                if (result.UtilizedHours + item.TotalEstimatedHours <= maxAllowedHours)
                {
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
                    // Oversized for remaining capacity — skip but keep scanning smaller ones.
                    result.ExcludedStories.Add(new ExcludedStoryDto
                    {
                        StoryId = story.Id,
                        TitleEn = story.TitleEn,
                        Reason = "Excluded due to sprint capacity limits."
                    });
                }

                // Unlock dependents whose only blocking dependency was this story.
                foreach (var dependentId in dependents[story.Id])
                {
                    inDegree[dependentId]--;
                    if (inDegree[dependentId] == 0 && itemById.TryGetValue(dependentId, out var depItem))
                    {
                        pq.Add(depItem);
                    }
                }
            }

            return result;
        }

        // ── Helper: when a story is excluded mid-loop (dependency blocked),
        //    propagate to its own dependents so they are dequeued and excluded too.
        private static void PropagateExclusion(
            Guid excludedId,
            Dictionary<Guid, List<Guid>> dependents,
            Dictionary<Guid, int> inDegree,
            Dictionary<Guid, StoryItem> itemById,
            SortedSet<StoryItem> pq,
            HashSet<Guid> fullyExcludedIds)
        {
            fullyExcludedIds.Add(excludedId);

            foreach (var childId in dependents[excludedId])
            {
                inDegree[childId]--;
                if (inDegree[childId] == 0 && itemById.TryGetValue(childId, out var childItem))
                {
                    // Add to pq so it reaches the main loop and gets excluded there
                    // (the defensive check above will catch it).
                    pq.Add(childItem);
                }
            }
        }

        // ── Fix 3: HasCycle — corrected finally-block timing.
        //    Previous bug: recursionStack.Remove(currentId) fired in the finally block
        //    before parent's check of recursionStack.Contains(currentId) ran (because the
        //    child's finally executes as part of the return-true unwind). This caused
        //    intermediate cycle nodes to be missed in cycleNodes.
        //
        //    Fix: capture cycle membership BEFORE returning true, while the recursion
        //    stack still contains the full current path. The finally block still cleans
        //    up the stack for the non-cycle path (backtracking).

        private static HashSet<Guid> DetectAndExcludeCycles(
            List<UserStory> backlog,
            List<ExcludedStoryDto> excludedStories)
        {
            var excludedIds = new HashSet<Guid>();
            var adjList = new Dictionary<Guid, Guid>();

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
                    var cycleNodes = new HashSet<Guid>();
                    HasCycle(story.Id, adjList, visited, recursionStack, cycleNodes);

                    foreach (var cycleNodeId in cycleNodes)
                    {
                        // Only try to look it up if it's actually in our backlog
                        // (the dep target might be a completed story not in this list).
                        var nodeStory = backlog.FirstOrDefault(s => s.Id == cycleNodeId);
                        if (nodeStory == null) continue;

                        if (excludedIds.Add(cycleNodeId))
                        {
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

            return excludedIds;
        }

        /// <summary>
        /// DFS cycle detector (Fix 3 — corrected).
        /// cycleNodes is populated with ALL nodes that are part of the cycle BEFORE any
        /// recursionStack.Remove() fires on the unwind path.
        /// Returns true if a cycle was detected rooted at (or reachable from) currentId.
        /// </summary>
        private static bool HasCycle(
            Guid currentId,
            Dictionary<Guid, Guid> adjList,
            HashSet<Guid> visited,
            HashSet<Guid> recursionStack,
            HashSet<Guid> cycleNodes)
        {
            visited.Add(currentId);
            recursionStack.Add(currentId);

            if (adjList.TryGetValue(currentId, out Guid depId))
            {
                if (!visited.Contains(depId))
                {
                    if (HasCycle(depId, adjList, visited, recursionStack, cycleNodes))
                    {
                        // A cycle was detected deeper in the stack.
                        // If currentId is still in the recursionStack, it is part of
                        // (or leads into) the cycle — record it now, before Remove() fires.
                        if (recursionStack.Contains(currentId))
                        {
                            cycleNodes.Add(currentId);
                        }

                        // Remove AFTER recording so the parent call's check is still valid.
                        recursionStack.Remove(currentId);
                        return true;
                    }
                }
                else if (recursionStack.Contains(depId))
                {
                    // Back-edge found — this IS the closing edge of the cycle.
                    // Record both the target (depId) and the current node before unwinding.
                    cycleNodes.Add(depId);
                    cycleNodes.Add(currentId);

                    recursionStack.Remove(currentId);
                    return true;
                }
            }

            // No cycle through currentId — clean backtrack.
            recursionStack.Remove(currentId);
            return false;
        }

        private static int GetPriorityValue(StoryPriority priority)
        {
            return priority switch
            {
                StoryPriority.Critical => 400,
                StoryPriority.High     => 300,
                StoryPriority.Medium   => 200,
                StoryPriority.Low      => 100,
                _                      => 0
            };
        }

        // ── Internal value-type to hold enriched story metadata.
        //    Used as SortedSet element; the custom comparer ensures uniqueness
        //    by including Story.Id as the final tie-break.
        private sealed class StoryItem
        {
            public UserStory Story { get; }
            public decimal TotalEstimatedHours { get; }
            public bool IsCarryOver { get; }

            public StoryItem(UserStory story, decimal totalEstimatedHours, bool isCarryOver)
            {
                Story = story;
                TotalEstimatedHours = totalEstimatedHours;
                IsCarryOver = isCarryOver;
            }
        }
    }
}
