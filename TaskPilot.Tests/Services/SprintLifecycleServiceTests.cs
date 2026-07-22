using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class SprintLifecycleServiceTests
    {
        private readonly Mock<ISprintRepository> _sprintRepoMock;
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IRepository<SprintRiskAlert>> _sprintRiskAlertRepoMock;
        private readonly Mock<ILogger<SprintLifecycleService>> _loggerMock;
        private readonly SprintLifecycleService _service;

        public SprintLifecycleServiceTests()
        {
            _sprintRepoMock = new Mock<ISprintRepository>();
            _projectRepoMock = new Mock<IRepository<Project>>();
            _sprintRiskAlertRepoMock = new Mock<IRepository<SprintRiskAlert>>();
            _loggerMock = new Mock<ILogger<SprintLifecycleService>>();
            _service = new SprintLifecycleService(
                _sprintRepoMock.Object,
                _projectRepoMock.Object,
                _sprintRiskAlertRepoMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task StartSprintAsync_PlannedSprint_ReturnsSuccess()
        {
            var projectId = Guid.NewGuid();
            var sprintId = Guid.NewGuid();
            var project = new Project();
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);
            var sprint = new Sprint { ProjectId = projectId, Status = SprintStatus.Planned };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(sprint, sprintId);

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);
            _sprintRepoMock.Setup(r => r.GetByIdAsync(sprintId)).ReturnsAsync(sprint);
            _sprintRepoMock.Setup(r => r.GetActiveSprintByProjectIdAsync(projectId, It.IsAny<CancellationToken>())).ReturnsAsync((Sprint?)null);

            var result = await _service.StartSprintAsync(projectId, sprintId);

            Assert.True(result.IsSuccess);
            Assert.Equal(SprintStatus.Active, sprint.Status);
            Assert.Equal(SprintStatus.Active.ToString(), result.Value!.Status);
        }

        [Fact]
        public async Task StartSprintAsync_AnotherActiveSprint_ReturnsFailure()
        {
            var projectId = Guid.NewGuid();
            var sprintId = Guid.NewGuid();
            var project = new Project();
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);
            var sprint = new Sprint { ProjectId = projectId, Status = SprintStatus.Planned };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(sprint, sprintId);
            var activeSprint = new Sprint { ProjectId = projectId, Status = SprintStatus.Active };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(activeSprint, Guid.NewGuid());

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);
            _sprintRepoMock.Setup(r => r.GetByIdAsync(sprintId)).ReturnsAsync(sprint);
            _sprintRepoMock.Setup(r => r.GetActiveSprintByProjectIdAsync(projectId, It.IsAny<CancellationToken>())).ReturnsAsync(activeSprint);

            var result = await _service.StartSprintAsync(projectId, sprintId);

            Assert.False(result.IsSuccess);
            Assert.Equal(SprintErrors.AnotherSprintAlreadyActive.Code, result.Error!.Code);
        }

        [Fact]
        public async Task CompleteSprintAsync_ActiveSprint_ReturnsSuccessAndUpdatesTasks()
        {
            var projectId = Guid.NewGuid();
            var sprintId = Guid.NewGuid();
            
            var task1 = new TaskItem { Status = TaskItemStatus.InProgress };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(task1, Guid.NewGuid());
            var task2 = new TaskItem { Status = TaskItemStatus.Done };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(task2, Guid.NewGuid());
            
            var sprint = new Sprint { 
                ProjectId = projectId, 
                Status = SprintStatus.Active,
                Tasks = new List<TaskItem> { task1, task2 }
            };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(sprint, sprintId);

            _sprintRepoMock.Setup(r => r.GetSprintWithTasksAsync(sprintId, It.IsAny<CancellationToken>())).ReturnsAsync(sprint);

            var result = await _service.CompleteSprintAsync(projectId, sprintId);

            Assert.True(result.IsSuccess);
            Assert.Equal(SprintStatus.Completed, sprint.Status);
            Assert.Equal(TaskItemStatus.ToDo, task1.Status);
            Assert.Equal(TaskItemStatus.Done, task2.Status);
        }

        [Fact]
        public async Task GetActiveSprintAsync_CalculatesDaysAndPercentageCorrectly()
        {
            var projectId = Guid.NewGuid();
            var sprintId = Guid.NewGuid();
            var project = new Project();
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);
            var activeSprint = new Sprint 
            { 
                ProjectId = projectId, 
                Status = SprintStatus.Active,
                EndDate = DateTime.UtcNow.AddDays(5)
            };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(activeSprint, sprintId);
            
            var task1 = new TaskItem { Status = TaskItemStatus.Done };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(task1, Guid.NewGuid());
            var task2 = new TaskItem { Status = TaskItemStatus.Done };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(task2, Guid.NewGuid());
            var task3 = new TaskItem { Status = TaskItemStatus.ToDo };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(task3, Guid.NewGuid());
            var sprintWithTasks = new Sprint 
            { 
                Tasks = new List<TaskItem> { task1, task2, task3 }
            };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(sprintWithTasks, sprintId);

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);
            _sprintRepoMock.Setup(r => r.GetActiveSprintByProjectIdAsync(projectId, It.IsAny<CancellationToken>())).ReturnsAsync(activeSprint);
            _sprintRepoMock.Setup(r => r.GetSprintWithTasksAsync(sprintId, It.IsAny<CancellationToken>())).ReturnsAsync(sprintWithTasks);

            var result = await _service.GetActiveSprintAsync(projectId);

            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Value!.DaysRemaining);
            Assert.Equal(66.67, result.Value!.CompletionPercentage); // 2 out of 3 tasks
        }
    }
}
