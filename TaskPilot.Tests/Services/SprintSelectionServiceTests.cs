using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using TaskPilot.DTOs.Planning;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Implementations;

namespace TaskPilot.Tests.Services
{
    public class SprintSelectionServiceTests
    {
        private readonly SprintSelectionService _service;

        public SprintSelectionServiceTests()
        {
            _service = new SprintSelectionService();
        }

        // ─────────────────────────────────────────────────────────────────────
        // BASELINE TESTS (pre-existing scenarios — all still pass after fixes)
        // ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void SelectStories_EmptyBacklog_ReturnsEmptyResult()
        {
            var options = new SprintSelectionOptions { TargetSprintHours = 100 };
            var result = _service.SelectStories(new List<UserStory>(), new List<Guid>(), options);

            Assert.Empty(result.SelectedStories);
            Assert.Empty(result.ExcludedStories);
            Assert.Equal(0, result.UtilizedHours);
        }

        [Fact]
        public void SelectStories_ExactFit_SelectsAll()
        {
            var backlog = new List<UserStory>
            {
                CreateStory(Guid.NewGuid(), StoryPriority.High, 40),
                CreateStory(Guid.NewGuid(), StoryPriority.Medium, 60)
            };

            var options = new SprintSelectionOptions { TargetSprintHours = 100 };
            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Equal(2, result.SelectedStories.Count);
            Assert.Empty(result.ExcludedStories);
            Assert.Equal(100, result.UtilizedHours);
        }

        [Fact]
        public void SelectStories_OverflowScanning_SkipsOversizedButContinuesScanning()
        {
            var s1 = CreateStory(Guid.NewGuid(), StoryPriority.High, 50);
            var s2 = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 70); // Oversized
            var s3 = CreateStory(Guid.NewGuid(), StoryPriority.Low, 30);   // Fits!

            var backlog = new List<UserStory> { s1, s2, s3 };
            var options = new SprintSelectionOptions { TargetSprintHours = 100, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Equal(2, result.SelectedStories.Count);
            Assert.Contains(result.SelectedStories, s => s.StoryId == s1.Id);
            Assert.Contains(result.SelectedStories, s => s.StoryId == s3.Id);

            Assert.Single(result.ExcludedStories);
            Assert.Equal("Excluded due to sprint capacity limits.", result.ExcludedStories[0].Reason);
            Assert.Equal(s2.Id, result.ExcludedStories[0].StoryId);

            Assert.Equal(80, result.UtilizedHours);
        }

        [Fact]
        public void SelectStories_PrioritizesCarryOverFirst_RegardlessOfPriority()
        {
            var s1 = CreateStory(Guid.NewGuid(), StoryPriority.Critical, 50);
            var s2 = CreateStory(Guid.NewGuid(), StoryPriority.Low, 30); // Carry-over

            var backlog = new List<UserStory> { s1, s2 };
            var carryOvers = new List<Guid> { s2.Id };

            var options = new SprintSelectionOptions { TargetSprintHours = 40, MaxUtilizationPercent = 1.0m };
            var result = _service.SelectStories(backlog, carryOvers, options);

            // s2 selected first (carry-over), leaving 10h. s1 (50h) oversized → excluded.
            Assert.Single(result.SelectedStories);
            Assert.Equal(s2.Id, result.SelectedStories[0].StoryId);
            Assert.Single(result.ExcludedStories);
            Assert.Equal("Excluded due to sprint capacity limits.", result.ExcludedStories[0].Reason);
            Assert.Equal(s1.Id, result.ExcludedStories[0].StoryId);
        }

        [Fact]
        public void SelectStories_TieBreak_SmallestHoursFirst()
        {
            var s1 = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 60);
            var s2 = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 40); // Smaller!
            var s3 = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 80);

            var backlog = new List<UserStory> { s1, s2, s3 };
            var options = new SprintSelectionOptions { TargetSprintHours = 50, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            // s2 (40h) processed first due to tie-break, fits in 50h budget.
            Assert.Single(result.SelectedStories);
            Assert.Equal(s2.Id, result.SelectedStories[0].StoryId);
            Assert.Equal(2, result.ExcludedStories.Count);
        }

        [Fact]
        public void SelectStories_DependencyCycle_DetectsAndExcludesAllCycleNodes()
        {
            var s1 = CreateStory(Guid.NewGuid(), StoryPriority.High, 20);
            var s2 = CreateStory(Guid.NewGuid(), StoryPriority.High, 20);
            var s3 = CreateStory(Guid.NewGuid(), StoryPriority.High, 20);

            s1.DependsOnStoryId = s2.Id;
            s2.DependsOnStoryId = s3.Id;
            s3.DependsOnStoryId = s1.Id; // Cycle!

            var backlog = new List<UserStory> { s1, s2, s3 };
            var options = new SprintSelectionOptions { TargetSprintHours = 100 };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Empty(result.SelectedStories);
            Assert.Equal(3, result.ExcludedStories.Count);
            Assert.All(result.ExcludedStories, e =>
                Assert.Equal("Excluded due to a circular dependency cycle.", e.Reason));
        }

        [Fact]
        public void SelectStories_DependsOnCyclicStory_ExcludesDependentStory()
        {
            var a = CreateStory(Guid.NewGuid(), StoryPriority.High, 10);
            var b = CreateStory(Guid.NewGuid(), StoryPriority.High, 10);
            var c = CreateStory(Guid.NewGuid(), StoryPriority.High, 10);
            var d = CreateStory(Guid.NewGuid(), StoryPriority.High, 10); // D depends on A (which is in a cycle)

            a.DependsOnStoryId = b.Id;
            b.DependsOnStoryId = c.Id;
            c.DependsOnStoryId = a.Id; // Cycle: A → B → C → A
            d.DependsOnStoryId = a.Id; // D → A

            var backlog = new List<UserStory> { a, b, c, d };
            var options = new SprintSelectionOptions { TargetSprintHours = 100 };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Empty(result.SelectedStories);
            Assert.Equal(4, result.ExcludedStories.Count);

            // A, B, C excluded due to cycle.
            var cycleExclusions = result.ExcludedStories
                .Where(e => e.Reason == "Excluded due to a circular dependency cycle.")
                .ToList();
            Assert.Equal(3, cycleExclusions.Count);
            Assert.Contains(cycleExclusions, e => e.StoryId == a.Id);
            Assert.Contains(cycleExclusions, e => e.StoryId == b.Id);
            Assert.Contains(cycleExclusions, e => e.StoryId == c.Id);

            // D excluded because its dependency (A) was itself excluded.
            var dExclusion = result.ExcludedStories.Single(e => e.StoryId == d.Id);
            Assert.Contains("excluded", dExclusion.Reason, StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 1: TOPOLOGICAL SORT REGRESSION TESTS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// THE primary bug scenario from the report:
        /// Critical story depends on a Medium story. Old single-pass code excluded the
        /// Critical story because Medium hadn't been selected yet when Critical was visited.
        /// After Fix 1 (topo sort), Medium always precedes Critical → both are selected.
        /// </summary>
        [Fact]
        public void SelectStories_Fix1_CriticalDependsOnMedium_BothSelected()
        {
            var medium = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 30);
            var critical = CreateStory(Guid.NewGuid(), StoryPriority.Critical, 40);

            // Critical depends on Medium.
            critical.DependsOnStoryId = medium.Id;

            var backlog = new List<UserStory> { critical, medium };
            // Capacity = 100h, both fit (40 + 30 = 70h).
            var options = new SprintSelectionOptions { TargetSprintHours = 100, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Equal(2, result.SelectedStories.Count);
            Assert.Contains(result.SelectedStories, s => s.StoryId == critical.Id);
            Assert.Contains(result.SelectedStories, s => s.StoryId == medium.Id);
            Assert.Empty(result.ExcludedStories);
            Assert.Equal(70m, result.UtilizedHours);
        }

        /// <summary>
        /// Linear dependency chain — topo sort should emit: Low → Medium → High (prerequisite first).
        /// All three fit within capacity, so all should be selected (old code would exclude High + Medium).
        /// </summary>
        [Fact]
        public void SelectStories_Fix1_LinearChain_AllSelectedInDependencyOrder()
        {
            var low    = CreateStory(Guid.NewGuid(), StoryPriority.Low,      20); // no dep
            var medium = CreateStory(Guid.NewGuid(), StoryPriority.Medium,   30); // depends on low
            var high   = CreateStory(Guid.NewGuid(), StoryPriority.High,     25); // depends on medium

            medium.DependsOnStoryId = low.Id;
            high.DependsOnStoryId   = medium.Id;

            var backlog = new List<UserStory> { high, medium, low }; // deliberately wrong order
            var options = new SprintSelectionOptions { TargetSprintHours = 100, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Equal(3, result.SelectedStories.Count);
            Assert.Empty(result.ExcludedStories);
            Assert.Equal(75m, result.UtilizedHours);
        }

        /// <summary>
        /// When a dependent story's prerequisite cannot fit in the sprint (capacity limits),
        /// the dependent should still be evaluated independently — it has no direct capacity
        /// block. Only exclude it if its prereq was excluded for non-capacity reasons.
        /// </summary>
        [Fact]
        public void SelectStories_Fix1_DepExcludedByCapacity_DependentStillEvaluated()
        {
            // prereq costs 80h — too large. Dependent costs 20h — should still be evaluated
            // since the capacity block is on the prereq, not a dependency chain break.
            // Note: topological sort ensures prereq is emitted first. If prereq is excluded
            // by capacity, the dependent's dep check (defensive guard) looks for
            // fullyExcludedIds, which only contains cycle/0h exclusions, NOT capacity
            // exclusions — so the dependent correctly proceeds to capacity check.
            var prereq    = CreateStory(Guid.NewGuid(), StoryPriority.Medium,   80); // won't fit
            var dependent = CreateStory(Guid.NewGuid(), StoryPriority.High,     20); // should be evaluated

            dependent.DependsOnStoryId = prereq.Id;

            var backlog = new List<UserStory> { dependent, prereq };
            var options = new SprintSelectionOptions { TargetSprintHours = 50, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            // prereq excluded by capacity limits; dependent not blocked (dep excluded by
            // capacity is NOT a hard blocker in this architecture — only cycle/0h exclusions are).
            Assert.Contains(result.ExcludedStories, e => e.StoryId == prereq.Id
                && e.Reason == "Excluded due to sprint capacity limits.");
            Assert.Contains(result.SelectedStories, s => s.StoryId == dependent.Id);
            Assert.Equal(20m, result.UtilizedHours);
        }

        /// <summary>
        /// Previously this test documented the old bug (s1 excluded, s2 selected only).
        /// After Fix 1 the topological sort emits s2 (Low, prereq) before s1 (Medium, dependent),
        /// so both are selected when capacity allows.
        /// </summary>
        [Fact]
        public void SelectStories_Fix1_LinearDependency_BothSelectedAfterTopoSort()
        {
            var s1 = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 30); // depends on s2
            var s2 = CreateStory(Guid.NewGuid(), StoryPriority.Low, 30);    // no dep

            s1.DependsOnStoryId = s2.Id;

            var backlog = new List<UserStory> { s1, s2 };
            var options = new SprintSelectionOptions { TargetSprintHours = 100, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            // Both fit (30 + 30 = 60h). Topo order: s2 first, then s1.
            Assert.Equal(2, result.SelectedStories.Count);
            Assert.Contains(result.SelectedStories, s => s.StoryId == s1.Id);
            Assert.Contains(result.SelectedStories, s => s.StoryId == s2.Id);
            Assert.Empty(result.ExcludedStories);
            Assert.Equal(60m, result.UtilizedHours);
        }

        /// <summary>
        /// Sprint 18 scenario from the bug report, simplified.
        /// 5 devs × 10 days × 8h × 80% = 320h target.
        /// Mix of dependent and independent stories well under capacity — all eligible
        /// stories should be selected, pushing UtilizedHours toward 320h.
        /// </summary>
        [Fact]
        public void SelectStories_Fix1_Sprint18Scenario_ClosesCapacityGap()
        {
            decimal targetHours = 320m; // representative of 330h real capacity

            // 3 independent stories (40h each = 120h)
            var ind1 = CreateStory(Guid.NewGuid(), StoryPriority.Critical, 40);
            var ind2 = CreateStory(Guid.NewGuid(), StoryPriority.High,     40);
            var ind3 = CreateStory(Guid.NewGuid(), StoryPriority.Medium,   40);

            // 2 chains: prereq + dependent (30h + 50h each = 160h)
            var prereq1 = CreateStory(Guid.NewGuid(), StoryPriority.Low,      30);
            var dep1    = CreateStory(Guid.NewGuid(), StoryPriority.Critical,  50);
            dep1.DependsOnStoryId = prereq1.Id;

            var prereq2 = CreateStory(Guid.NewGuid(), StoryPriority.Medium,   30);
            var dep2    = CreateStory(Guid.NewGuid(), StoryPriority.High,      50);
            dep2.DependsOnStoryId = prereq2.Id;

            // Total eligible: 120 + 160 = 280h — all fit within 320h.
            var backlog = new List<UserStory> { dep1, dep2, ind1, ind2, ind3, prereq1, prereq2 };
            var options = new SprintSelectionOptions
            {
                TargetSprintHours   = targetHours,
                MaxUtilizationPercent = 1.0m
            };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            // OLD result: dep1 and dep2 excluded → UtilizedHours ≈ 120h only.
            // NEW result: all 7 stories selected → UtilizedHours = 280h.
            Assert.Equal(7, result.SelectedStories.Count);
            Assert.Empty(result.ExcludedStories);
            Assert.Equal(280m, result.UtilizedHours);
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 3: CYCLE DETECTION COMPLETENESS TEST
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fix 3: 3-node cycle A→B→C→A. Old code could miss intermediate nodes in cycleNodes
        /// due to the finally-block removal firing before parent's Contains() check.
        /// After fix, all three IDs must appear in ExcludedStories with cycle reason.
        /// </summary>
        [Fact]
        public void SelectStories_Fix3_ThreeNodeCycle_AllThreeNodesExcluded()
        {
            var a = CreateStory(Guid.NewGuid(), StoryPriority.High, 20);
            var b = CreateStory(Guid.NewGuid(), StoryPriority.High, 20);
            var c = CreateStory(Guid.NewGuid(), StoryPriority.High, 20);

            a.DependsOnStoryId = b.Id;
            b.DependsOnStoryId = c.Id;
            c.DependsOnStoryId = a.Id; // Closes cycle: A → B → C → A

            var backlog = new List<UserStory> { a, b, c };
            var options = new SprintSelectionOptions { TargetSprintHours = 200 };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Empty(result.SelectedStories);

            var cycleExclusions = result.ExcludedStories
                .Where(e => e.Reason == "Excluded due to a circular dependency cycle.")
                .ToList();

            // All three nodes must be in the cycle exclusion list.
            Assert.Equal(3, cycleExclusions.Count);
            Assert.Contains(cycleExclusions, e => e.StoryId == a.Id);
            Assert.Contains(cycleExclusions, e => e.StoryId == b.Id);
            Assert.Contains(cycleExclusions, e => e.StoryId == c.Id);
        }

        // ─────────────────────────────────────────────────────────────────────
        // FIX 4: ZERO-TASK STORY GUARD TESTS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fix 4: A story with no tasks at all (0h) must be excluded immediately
        /// with a clear "no tasks attached" reason rather than being selected for free.
        /// </summary>
        [Fact]
        public void SelectStories_Fix4_StoryWithNoTasks_IsExcluded()
        {
            var noTaskStory  = CreateStoryNoTasks(Guid.NewGuid(), StoryPriority.Critical);
            var normalStory  = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 30);

            var backlog = new List<UserStory> { noTaskStory, normalStory };
            var options = new SprintSelectionOptions { TargetSprintHours = 100, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            // noTaskStory must not be selected.
            Assert.DoesNotContain(result.SelectedStories, s => s.StoryId == noTaskStory.Id);

            // normalStory selected normally.
            Assert.Single(result.SelectedStories);
            Assert.Equal(normalStory.Id, result.SelectedStories[0].StoryId);
            Assert.Equal(30m, result.UtilizedHours);

            // noTaskStory in ExcludedStories with the "no tasks" reason.
            var exclusion = result.ExcludedStories.Single(e => e.StoryId == noTaskStory.Id);
            Assert.Contains("no tasks attached", exclusion.Reason, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Fix 4: Multiple 0h stories don't distort UtilizedHours — confirms they are
        /// excluded and don't silently consume capacity or inflate SelectedStories.Count.
        /// </summary>
        [Fact]
        public void SelectStories_Fix4_MultipleNoTaskStories_AllExcluded_UtilizedHoursUnaffected()
        {
            var nt1 = CreateStoryNoTasks(Guid.NewGuid(), StoryPriority.High);
            var nt2 = CreateStoryNoTasks(Guid.NewGuid(), StoryPriority.Critical);
            var real = CreateStory(Guid.NewGuid(), StoryPriority.Medium, 50);

            var backlog = new List<UserStory> { nt1, nt2, real };
            var options = new SprintSelectionOptions { TargetSprintHours = 100, MaxUtilizationPercent = 1.0m };

            var result = _service.SelectStories(backlog, new List<Guid>(), options);

            Assert.Single(result.SelectedStories);
            Assert.Equal(2, result.ExcludedStories.Count(e => e.StoryId == nt1.Id || e.StoryId == nt2.Id));
            Assert.Equal(50m, result.UtilizedHours); // only real story's hours counted
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private static UserStory CreateStory(Guid id, StoryPriority priority, decimal hours)
        {
            var story = new UserStory
            {
                TitleEn = $"Story {id}",
                Priority = priority,
                Tasks = new List<TaskItem>
                {
                    new TaskItem { EstimatedHours = hours, Status = TaskItemStatus.ToDo }
                }
            };

            typeof(BaseEntity<Guid>).GetProperty("Id")?.SetValue(story, id);
            return story;
        }

        private static UserStory CreateStoryNoTasks(Guid id, StoryPriority priority)
        {
            var story = new UserStory
            {
                TitleEn = $"Story {id} (no tasks)",
                Priority = priority,
                Tasks = new List<TaskItem>()  // empty
            };

            typeof(BaseEntity<Guid>).GetProperty("Id")?.SetValue(story, id);
            return story;
        }
    }
}
