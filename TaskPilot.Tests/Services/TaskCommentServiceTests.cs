using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Tasks.Comments;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Implementations;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class TaskCommentServiceTests
    {
        private readonly Mock<IRepository<TaskComment>> _commentRepoMock;
        private readonly Mock<IRepository<TaskItem>> _taskRepoMock;
        private readonly Mock<IRepository<User>> _userRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmployeeRepoMock;
        private readonly Mock<ILogger<TaskCommentService>> _loggerMock;
        private readonly TaskCommentService _service;

        public TaskCommentServiceTests()
        {
            _commentRepoMock = new Mock<IRepository<TaskComment>>();
            _taskRepoMock = new Mock<IRepository<TaskItem>>();
            _userRepoMock = new Mock<IRepository<User>>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();
            _loggerMock = new Mock<ILogger<TaskCommentService>>();

            _service = new TaskCommentService(
                _commentRepoMock.Object,
                _taskRepoMock.Object,
                _userRepoMock.Object,
                _projectEmployeeRepoMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task AddCommentAsync_Success_WhenPM()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = projectId };
            var task = new TaskItem { Sprint = sprint, EmployeeId = Guid.NewGuid() };
            var user = new ProjectManager { FirstNameEn = "PM", LastNameEn = "User" };

            _taskRepoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<Expression<Func<TaskItem, object>>[]>() ))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(r => r.IsProjectManagerAsync(projectId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _userRepoMock.Setup(r => r.GetByIdAsync(userId))
                .ReturnsAsync(user);

            var request = new AddTaskCommentRequest { Content = "Nice job!" };

            // Act
            var result = await _service.AddCommentAsync(taskId, userId, request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Nice job!", result.Value.Content);
            Assert.Equal("ProjectManager", result.Value.AuthorRole);
            _commentRepoMock.Verify(r => r.AddAsync(It.IsAny<TaskComment>()), Times.Once);
        }

        [Fact]
        public async Task AddCommentAsync_Forbidden_WhenNotPMOrAssignee()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = projectId };
            var task = new TaskItem { Sprint = sprint, EmployeeId = Guid.NewGuid() };

            _taskRepoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<Expression<Func<TaskItem, object>>[]>() ))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(r => r.IsProjectManagerAsync(projectId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new AddTaskCommentRequest { Content = "Nice job!" };

            // Act
            var result = await _service.AddCommentAsync(taskId, userId, request);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(TaskErrors.ForbiddenTaskUpdate.Code, result.Error.Code);
        }

        [Fact]
        public async Task GetCommentsAsync_Success()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = projectId };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId };

            _taskRepoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<Expression<Func<TaskItem, object>>[]>() ))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(r => r.IsProjectManagerAsync(projectId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var author = new Employee { FirstNameEn = "Emp", LastNameEn = "User" };
            var comments = new List<TaskComment>
            {
                new TaskComment { TaskId = taskId, Content = "Hello", User = author, UserId = Guid.NewGuid() }
            };

            _commentRepoMock.Setup(r => r.GetQueryable())
                .Returns(comments.BuildMockDbSet().Object);

            // Act
            var result = await _service.GetCommentsAsync(taskId, userId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Single(result.Value);
            Assert.Equal("Employee", result.Value.First().AuthorRole);
        }
    }
}
