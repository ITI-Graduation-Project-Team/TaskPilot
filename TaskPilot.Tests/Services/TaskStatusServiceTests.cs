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
        public async Task UpdateStatusAsync_InvalidTransition_ReturnsFailure()
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
            Assert.False(result.IsSuccess);
            Assert.Equal(TaskErrors.InvalidTaskStatusTransition.Code, result.Error.Code);
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
    }
}
