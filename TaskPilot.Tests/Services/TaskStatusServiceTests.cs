using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Tasks;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Implementations;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class TaskStatusServiceTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock;
        private readonly Mock<ISprintRepository> _sprintRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmployeeRepoMock;
        private readonly Mock<ILogger<TaskStatusService>> _loggerMock;
        private readonly TaskStatusService _service;

        public TaskStatusServiceTests()
        {
            _taskRepoMock = new Mock<ITaskRepository>();
            _sprintRepoMock = new Mock<ISprintRepository>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();
            _loggerMock = new Mock<ILogger<TaskStatusService>>();

            _service = new TaskStatusService(
                _taskRepoMock.Object,
                _sprintRepoMock.Object,
                _projectEmployeeRepoMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task GetMyTasksAsync_SprintNotActive_ReturnsFailure()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _sprintRepoMock.Setup(x => x.GetActiveSprintByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Sprint?)null);

            // Act
            var result = await _service.GetMyTasksAsync(projectId, userId);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(TaskErrors.ActiveSprintNotFound.Code, result.Error.Code);
        }

        [Fact]
        public async Task UpdateStatusAsync_ToDoToDone_ReturnsSuccess()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.ToDo };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);
            
            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.Done, ActualHours = 5 };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(TaskItemStatus.Done, result.Value.NewStatus);
        }
        
        [Fact]
        public async Task UpdateStatusAsync_ValidTransition_ReturnsSuccess()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.ToDo };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);
            
            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.InProgress };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(TaskItemStatus.InProgress, result.Value.NewStatus);
        }

        [Fact]
        public async Task UpdateStatusAsync_InProgressToReview_ReturnsSuccess()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.InProgress };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);
            
            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.Review };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(TaskItemStatus.Review, result.Value.NewStatus);
        }

        [Fact]
        public async Task UpdateStatusAsync_ReviewToDone_ReturnsSuccess()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.Review };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);
            
            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.Done, ActualHours = 4 };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(TaskItemStatus.Done, result.Value.NewStatus);
            Assert.Equal(4, result.Value.ActualHours);
        }

        [Fact]
        public async Task UpdateStatusAsync_ReviewToInProgress_ReturnsSuccess()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.Review };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);
            
            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.InProgress };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(TaskItemStatus.InProgress, result.Value.NewStatus);
        }

        [Fact]
        public async Task UpdateStatusAsync_ToDoToReview_ReturnsSuccess()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.ToDo };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);
            
            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.Review };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(TaskItemStatus.Review, result.Value.NewStatus);
        }

        [Fact]
        public async Task UpdateStatusAsync_InProgressToDone_AutoCalculatesWorkingHours()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            
            // Set InProgressAt to exactly 2 hours ago within working hours (e.g. 10:00 AM to 12:00 PM today)
            // To ensure it is stable, let's pick a fixed working hours window.
            // Let's set InProgressAt to a fixed date at 10 AM, and mock/pass DateTime.UtcNow equivalent inside the logic,
            // or just compute a relative offset. Since the logic uses DateTime.UtcNow for current time:
            // Let's set it to 2 hours ago.
            var start = DateTime.UtcNow.Date.AddHours(10); // 10:00 AM today
            if (DateTime.UtcNow < start.AddHours(2))
            {
                // If current time is earlier, use yesterday
                start = DateTime.UtcNow.AddDays(-1).Date.AddHours(10);
            }
            
            var task = new TaskItem 
            { 
                Sprint = sprint, 
                EmployeeId = userId, 
                Status = TaskItemStatus.InProgress,
                InProgressAt = start
            };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);

            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.Done };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(TaskItemStatus.Done, result.Value.NewStatus);
            Assert.True(result.Value.ActualHours >= 0);
        }

        [Fact]
        public async Task UpdateStatusAsync_DoneWithoutInProgressAt_FallsBackToManualHours()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.ToDo, InProgressAt = null };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);

            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.Done, ActualHours = 3.5m };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(3.5m, result.Value.ActualHours);
        }

        [Fact]
        public async Task UpdateStatusAsync_DoneWithoutInProgressAtOrManualHours_ReturnsActualHoursRequired()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = Guid.NewGuid(), Status = SprintStatus.Active };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId, Status = TaskItemStatus.ToDo, InProgressAt = null };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, taskId);

            _taskRepoMock.Setup(x => x.GetByIdWithSprintAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(x => x.IsProjectManagerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new UpdateTaskStatusRequest { Status = TaskItemStatus.Done, ActualHours = null };

            // Act
            var result = await _service.UpdateStatusAsync(taskId, userId, request);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(TaskErrors.ActualHoursRequired.Code, result.Error.Code);
        }
    }
}
