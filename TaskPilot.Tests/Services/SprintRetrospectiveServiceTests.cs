using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.Moq;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using TaskPilot.Services.Implementations;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class SprintRetrospectiveServiceTests
    {
        private readonly Mock<IRepository<Sprint>> _sprintRepoMock;
        private readonly Mock<IRepository<TaskItem>> _taskRepoMock;
        private readonly Mock<IRepository<SprintRiskAlert>> _riskAlertRepoMock;
        private readonly Mock<IRepository<SprintRetrospective>> _retrospectiveRepoMock;

        public SprintRetrospectiveServiceTests()
        {
            _sprintRepoMock        = new Mock<IRepository<Sprint>>();
            _taskRepoMock          = new Mock<IRepository<TaskItem>>();
            _riskAlertRepoMock     = new Mock<IRepository<SprintRiskAlert>>();
            _retrospectiveRepoMock = new Mock<IRepository<SprintRetrospective>>();
        }

        private static T SetEntityId<T>(T entity, Guid id) where T : BaseEntity<Guid>
        {
            var prop = typeof(BaseEntity<Guid>).GetProperty(
                "Id", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(entity, id);
            return entity;
        }

        private SprintDataCollectionService BuildCollector()
            => new SprintDataCollectionService(
                _sprintRepoMock.Object,
                _taskRepoMock.Object,
                _riskAlertRepoMock.Object);

        private void SetupDoneTasks(List<TaskItem> doneTasks)
        {
            var mock = doneTasks.BuildMockDbSet();
            _taskRepoMock.Setup(r => r.GetQueryable()).Returns(mock.Object);
        }

        private void SetupRiskAlerts(List<SprintRiskAlert> alerts)
        {
            var mock = alerts.BuildMockDbSet();
            _riskAlertRepoMock.Setup(r => r.GetQueryable()).Returns(mock.Object);
        }

        // ────────────────────────────────────────────────────────────────────
        // Core regression test: 3 tasks, 2 Done, 1 unfinished (SprintId cleared)
        // This is the real-world scenario that was broken.
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CollectAsync_ThreeTasksTwoComplete_CountsAllTasksCorrectly()
        {
            var sprintId = Guid.NewGuid();
            var devId    = Guid.NewGuid();
            var dev      = new Employee { FirstNameEn = "Yasser", LastNameEn = "Essa" };

            var sprint = SetEntityId(new Sprint
            {
                Status    = SprintStatus.Completed,
                TitleEn   = "Sprint 1",
                StartDate = DateTime.UtcNow.AddDays(-14),
                EndDate   = DateTime.UtcNow
            }, sprintId);

            var storyId = Guid.NewGuid();
            var story   = SetEntityId(new UserStory { TitleEn = "Authentication Story", TitleAr = "قصة مصادقة الهوية" }, storyId);

            // Task 1 — Done (SprintId still set)
            var task1 = SetEntityId(new TaskItem
            {
                SprintId       = sprintId,
                UserStoryId    = storyId,
                UserStory      = story,
                Status         = TaskItemStatus.Done,
                EstimatedHours = 10,
                ActualHours    = 8,
                EmployeeId     = devId,
                Employee       = dev,
                TitleEn        = "Task 1",
                Type           = TaskType.Technical
            }, Guid.NewGuid());

            // Task 2 — Done (SprintId still set)
            var task2 = SetEntityId(new TaskItem
            {
                SprintId       = sprintId,
                UserStoryId    = storyId,
                UserStory      = story,
                Status         = TaskItemStatus.Done,
                EstimatedHours = 5,
                ActualHours    = 6,
                EmployeeId     = devId,
                Employee       = dev,
                TitleEn        = "Task 2",
                Type           = TaskType.Technical
            }, Guid.NewGuid());

            // Task 3 — Unfinished (SprintId was cleared at sprint completion)
            var task3Id = Guid.NewGuid();
            var task3 = SetEntityId(new TaskItem
            {
                SprintId       = null,   // ← cleared by sprint completion
                UserStoryId    = storyId,
                UserStory      = story,
                EmployeeId     = null,   // ← cleared by sprint completion
                Status         = TaskItemStatus.ToDo,
                EstimatedHours = 4,
                ActualHours    = 0,
                TitleEn        = "Task 3 (Unfinished)",
                Type           = TaskType.Technical
            }, task3Id);

            // SprintRiskAlert is the ONLY remaining link for unfinished tasks.
            // AffectedEmployeeId is preserved here even though task.EmployeeId was cleared.
            var alert = SetEntityId(new SprintRiskAlert
            {
                SprintId            = sprintId,
                RiskType            = SprintRiskType.UnfinishedTask,
                AffectedTaskId      = task3Id,
                AffectedTask        = task3,
                AffectedEmployeeId  = devId,   // ← saved before clearing
                AffectedEmployee    = dev,      // ← employee navigation saved
                LastDetectedAt      = DateTime.UtcNow
            }, Guid.NewGuid());

            _sprintRepoMock.Setup(r => r.GetByIdAsync(sprintId)).ReturnsAsync(sprint);
            SetupDoneTasks(new List<TaskItem> { task1, task2 });
            SetupRiskAlerts(new List<SprintRiskAlert> { alert });

            var result = await BuildCollector().CollectAsync(sprintId);

            // ── Totals ──
            Assert.Equal(3, result.TotalTasks);         // was wrongly 2 before fix
            Assert.Equal(2, result.CompletedTasks);
            Assert.Equal(0, result.InProgressTasks);
            Assert.Equal(1, result.NotStartedTasks);

            // CompletionRate = 2/3 ≈ 66.67
            Assert.Equal(Math.Round(2.0 / 3.0 * 100, 2), result.CompletionRate);

            // ── Hours (only Done tasks have actual hours) ──
            Assert.Equal(19, result.TotalEstimatedHours);  // 10+5+4
            Assert.Equal(14, result.TotalActualHours);     // 8+6

            // ── Developer breakdown — must reflect BOTH done and unfinished tasks ──
            Assert.Single(result.DeveloperBreakdowns);
            var devBreakdown = result.DeveloperBreakdowns[0];
            Assert.Equal("Yasser Essa", devBreakdown.FullName);
            Assert.Equal(3, devBreakdown.AssignedTasks);                                    // 2 done + 1 unfinished
            Assert.Equal(2, devBreakdown.CompletedTasks);
            Assert.Equal(Math.Round(2.0 / 3.0 * 100, 2), devBreakdown.CompletionRate);     // 66.67
            Assert.Equal(19m, devBreakdown.EstimatedHours);                                 // all 3 tasks
            Assert.Equal(14m, devBreakdown.ActualHours);                                    // done tasks only

            // ── Feature Completeness Index (Idea 5) ──
            Assert.Single(result.PartiallyCompletedStories);
            var partialStory = result.PartiallyCompletedStories[0];
            Assert.Equal(storyId, partialStory.UserStoryId);
            Assert.Equal("Authentication Story", partialStory.TitleEn);
            Assert.Equal(3, partialStory.TotalTasks);
            Assert.Equal(2, partialStory.CompletedTasks);
            Assert.Equal(1, partialStory.RemainingTasks);
            Assert.Equal(Math.Round(2.0 / 3.0 * 100, 1), partialStory.CompletionPercentage);

            // ── Unfinished tasks ──
            Assert.Single(result.UnfinishedTasks);
            Assert.Equal("Task 3 (Unfinished)", result.UnfinishedTasks[0].TitleEn);
            Assert.Equal("NotStarted", result.UnfinishedTasks[0].Reason);
        }

        // ────────────────────────────────────────────────────────────────────
        // All tasks Done — no SprintRiskAlerts
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CollectAsync_AllTasksDone_Returns100CompletionRate()
        {
            var sprintId = Guid.NewGuid();

            var sprint = SetEntityId(new Sprint
            {
                Status    = SprintStatus.Completed,
                TitleEn   = "Sprint 1",
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate   = DateTime.UtcNow
            }, sprintId);

            var task1 = SetEntityId(new TaskItem
            {
                SprintId       = sprintId,
                Status         = TaskItemStatus.Done,
                EstimatedHours = 8,
                ActualHours    = 8,
                TitleEn        = "Task A",
                Type           = TaskType.Technical
            }, Guid.NewGuid());

            _sprintRepoMock.Setup(r => r.GetByIdAsync(sprintId)).ReturnsAsync(sprint);
            SetupDoneTasks(new List<TaskItem> { task1 });
            SetupRiskAlerts(new List<SprintRiskAlert>());   // no alerts = no unfinished tasks

            var result = await BuildCollector().CollectAsync(sprintId);

            Assert.Equal(1, result.TotalTasks);
            Assert.Equal(1, result.CompletedTasks);
            Assert.Equal(100.0, result.CompletionRate);
            Assert.Empty(result.UnfinishedTasks);
        }

        // ────────────────────────────────────────────────────────────────────
        // No tasks Done, one unfinished captured via SprintRiskAlert
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CollectAsync_NoTasksDone_Returns0CompletionRate()
        {
            var sprintId = Guid.NewGuid();

            var sprint = SetEntityId(new Sprint
            {
                Status    = SprintStatus.Completed,
                TitleEn   = "Sprint 1",
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate   = DateTime.UtcNow
            }, sprintId);

            var taskId = Guid.NewGuid();
            var task = SetEntityId(new TaskItem
            {
                SprintId       = null,
                Status         = TaskItemStatus.ToDo,
                EstimatedHours = 6,
                ActualHours    = 0,
                TitleEn        = "Not-started task",
                Type           = TaskType.Technical
            }, taskId);

            var alert = SetEntityId(new SprintRiskAlert
            {
                SprintId       = sprintId,
                RiskType       = SprintRiskType.UnfinishedTask,
                AffectedTaskId = taskId,
                AffectedTask   = task,
                LastDetectedAt = DateTime.UtcNow
            }, Guid.NewGuid());

            _sprintRepoMock.Setup(r => r.GetByIdAsync(sprintId)).ReturnsAsync(sprint);
            SetupDoneTasks(new List<TaskItem>());
            SetupRiskAlerts(new List<SprintRiskAlert> { alert });

            var result = await BuildCollector().CollectAsync(sprintId);

            Assert.Equal(1, result.TotalTasks);
            Assert.Equal(0, result.CompletedTasks);
            Assert.Equal(0.0, result.CompletionRate);
            Assert.Single(result.UnfinishedTasks);
            Assert.Equal("NotStarted", result.UnfinishedTasks[0].Reason);
        }

        // ────────────────────────────────────────────────────────────────────
        // Duplicate alerts for the same task — de-duplicate correctly
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CollectAsync_DuplicateAlerts_DeduplicatesUnfinishedTasks()
        {
            var sprintId = Guid.NewGuid();

            var sprint = SetEntityId(new Sprint
            {
                Status    = SprintStatus.Completed,
                TitleEn   = "Sprint 1",
                StartDate = DateTime.UtcNow.AddDays(-7),
                EndDate   = DateTime.UtcNow
            }, sprintId);

            var taskId = Guid.NewGuid();
            var task = SetEntityId(new TaskItem
            {
                SprintId       = null,
                Status         = TaskItemStatus.ToDo,
                EstimatedHours = 6,
                TitleEn        = "Dup Task",
                Type           = TaskType.Technical
            }, taskId);

            // Same task appears twice in alerts (risk detection ran twice)
            var alert1 = SetEntityId(new SprintRiskAlert { SprintId = sprintId, RiskType = SprintRiskType.UnfinishedTask, AffectedTaskId = taskId, AffectedTask = task, LastDetectedAt = DateTime.UtcNow }, Guid.NewGuid());
            var alert2 = SetEntityId(new SprintRiskAlert { SprintId = sprintId, RiskType = SprintRiskType.UnfinishedTask, AffectedTaskId = taskId, AffectedTask = task, LastDetectedAt = DateTime.UtcNow }, Guid.NewGuid());

            _sprintRepoMock.Setup(r => r.GetByIdAsync(sprintId)).ReturnsAsync(sprint);
            SetupDoneTasks(new List<TaskItem>());
            SetupRiskAlerts(new List<SprintRiskAlert> { alert1, alert2 });

            var result = await BuildCollector().CollectAsync(sprintId);

            // Despite 2 alerts, should count 1 unique unfinished task
            Assert.Equal(1, result.TotalTasks);
            Assert.Single(result.UnfinishedTasks);
        }

        // ────────────────────────────────────────────────────────────────────
        // Non-completed sprint must throw
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task CollectAsync_NonCompletedSprint_ThrowsInvalidOperationException()
        {
            var sprintId = Guid.NewGuid();
            var sprint   = SetEntityId(
                new Sprint { Status = SprintStatus.Active, TitleEn = "Sprint 1" },
                sprintId);

            _sprintRepoMock.Setup(r => r.GetByIdAsync(sprintId)).ReturnsAsync(sprint);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => BuildCollector().CollectAsync(sprintId));
        }

        // ────────────────────────────────────────────────────────────────────
        // GetAsync returns null when retrospective not found
        // ────────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GetAsync_ReturnsNullWhenNotFound()
        {
            var sprintId = Guid.NewGuid();
            _retrospectiveRepoMock
                .Setup(r => r.FindAsync(
                    It.IsAny<Expression<Func<SprintRetrospective, bool>>>(),
                    It.IsAny<Expression<Func<SprintRetrospective, object>>[]>()))
                .ReturnsAsync(new List<SprintRetrospective>());

            var service = new SprintRetrospectiveService(
                _retrospectiveRepoMock.Object,
                null!,
                null!);

            var result = await service.GetAsync(sprintId);

            Assert.Null(result);
        }
    }
}
