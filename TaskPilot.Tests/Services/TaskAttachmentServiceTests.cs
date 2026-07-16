using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Common;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Implementations;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class TaskAttachmentServiceTests
    {
        private readonly Mock<IRepository<TaskAttachment>> _attachmentRepoMock;
        private readonly Mock<IRepository<TaskItem>> _taskRepoMock;
        private readonly Mock<IRepository<User>> _userRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmployeeRepoMock;
        private readonly Mock<IFileStorageService> _fileStorageMock;
        private readonly Mock<ILogger<TaskAttachmentService>> _loggerMock;
        private readonly TaskAttachmentService _service;

        public TaskAttachmentServiceTests()
        {
            _attachmentRepoMock = new Mock<IRepository<TaskAttachment>>();
            _taskRepoMock = new Mock<IRepository<TaskItem>>();
            _userRepoMock = new Mock<IRepository<User>>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();
            _fileStorageMock = new Mock<IFileStorageService>();
            _loggerMock = new Mock<ILogger<TaskAttachmentService>>();

            _service = new TaskAttachmentService(
                _attachmentRepoMock.Object,
                _taskRepoMock.Object,
                _userRepoMock.Object,
                _projectEmployeeRepoMock.Object,
                _fileStorageMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task UploadAttachmentAsync_FileTooLarge_ReturnsFailure()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(11 * 1024 * 1024); // 11 MB

            // Act
            var result = await _service.UploadAttachmentAsync(taskId, userId, fileMock.Object);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal(TaskAttachmentErrors.FileTooLarge.Code, result.Error.Code);
        }

        [Fact]
        public async Task UploadAttachmentAsync_Success()
        {
            // Arrange
            var taskId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var sprint = new Sprint { ProjectId = projectId };
            var task = new TaskItem { Sprint = sprint, EmployeeId = userId };
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(2 * 1024 * 1024); // 2 MB
            fileMock.Setup(f => f.FileName).Returns("doc.pdf");
            fileMock.Setup(f => f.ContentType).Returns("application/pdf");

            _taskRepoMock.Setup(r => r.GetByIdAsync(taskId, It.IsAny<Expression<Func<TaskItem, object>>[]>() ))
                .ReturnsAsync(task);
            _projectEmployeeRepoMock.Setup(r => r.IsProjectManagerAsync(projectId, userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var uploadResult = new FileUploadResultDto { Url = "http://cloudinary.com/doc.pdf", PublicId = "cloudinary_id" };
            _fileStorageMock.Setup(s => s.UploadFileAsync(fileMock.Object, It.IsAny<string>()))
                .ReturnsAsync(Result.Success(uploadResult));

            var uploader = new Employee { FirstNameEn = "Emp", LastNameEn = "User" };
            _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(uploader);

            // Act
            var result = await _service.UploadAttachmentAsync(taskId, userId, fileMock.Object);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("doc.pdf", result.Value.FileName);
            Assert.Equal("Employee", result.Value.UploaderRole);
            _attachmentRepoMock.Verify(r => r.AddAsync(It.IsAny<TaskAttachment>()), Times.Once);
        }
    }
}
