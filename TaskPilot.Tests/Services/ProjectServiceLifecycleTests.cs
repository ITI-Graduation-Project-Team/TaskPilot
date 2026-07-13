using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;
using Xunit;
using TaskPilot.Models.Common;
using System.Collections.Generic;

namespace TaskPilot.Tests.Services
{
    public class ProjectServiceLifecycleTests
    {
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IRepository<Company>> _companyRepoMock;
        private readonly Mock<IRepository<ProjectManager>> _managerRepoMock;
        private readonly Mock<ILocalizationService> _localizationMock;
        private readonly Mock<ILogger<ProjectService>> _loggerMock;
        private readonly ProjectService _service;

        public ProjectServiceLifecycleTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _companyRepoMock = new Mock<IRepository<Company>>();
            _managerRepoMock = new Mock<IRepository<ProjectManager>>();
            _localizationMock = new Mock<ILocalizationService>();
            _loggerMock = new Mock<ILogger<ProjectService>>();

            _service = new ProjectService(
                _projectRepoMock.Object,
                _companyRepoMock.Object,
                _managerRepoMock.Object,
                _localizationMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task GetStatusAsync_ValidId_ReturnsStatus()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { Status = ProjectStatus.Active };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);

            _projectRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<Project> { project }.AsQueryable());

            var result = await _service.GetStatusAsync(projectId);

            Assert.True(result.IsSuccess);
            Assert.Equal(ProjectStatus.Active, result.Value.Status);
        }

        [Fact]
        public async Task GetStatusAsync_InvalidId_ReturnsFailure()
        {
            var result = await _service.GetStatusAsync(Guid.Empty);

            Assert.False(result.IsSuccess);
            Assert.Equal(ProjectErrors.InvalidProjectId.Code, result.Error.Code);
        }

        [Fact]
        public async Task GetStatusAsync_NotFound_ReturnsFailure()
        {
            _projectRepoMock.Setup(r => r.GetQueryable()).Returns(new List<Project>().AsQueryable());

            var result = await _service.GetStatusAsync(Guid.NewGuid());

            Assert.False(result.IsSuccess);
            Assert.Equal(ProjectErrors.NotFound.Code, result.Error.Code);
        }

        [Fact]
        public async Task UpdateStatusAsync_ValidTransition_UpdatesStatus()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { Status = ProjectStatus.Draft };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);

            var result = await _service.UpdateStatusAsync(projectId, new ProjectStatusUpdateRequest { Status = ProjectStatus.Active }, "user1");

            Assert.True(result.IsSuccess);
            Assert.Equal(ProjectStatus.Active, project.Status);
            _projectRepoMock.Verify(r => r.Update(project), Times.Once);
        }

        [Fact]
        public async Task UpdateStatusAsync_InvalidTransition_ReturnsFailure()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { Status = ProjectStatus.Active };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);

            var result = await _service.UpdateStatusAsync(projectId, new ProjectStatusUpdateRequest { Status = ProjectStatus.Draft }, "user1");

            Assert.False(result.IsSuccess);
            Assert.Equal(ProjectErrors.InvalidStatusTransition.Code, result.Error.Code);
        }

        [Fact]
        public async Task UpdateStatusAsync_CompletedToActive_ReturnsFailure()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { Status = ProjectStatus.Completed };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);

            var result = await _service.UpdateStatusAsync(projectId, new ProjectStatusUpdateRequest { Status = ProjectStatus.Active }, "user1");

            Assert.False(result.IsSuccess);
            Assert.Equal(ProjectErrors.ProjectAlreadyCompleted.Code, result.Error.Code);
        }

        [Fact]
        public async Task UpdateStatusAsync_ArchivedToAnything_ReturnsFailure()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { Status = ProjectStatus.Archived };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(project, projectId);

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);

            var result = await _service.UpdateStatusAsync(projectId, new ProjectStatusUpdateRequest { Status = ProjectStatus.Active }, "user1");

            Assert.False(result.IsSuccess);
            Assert.Equal(ProjectErrors.ProjectAlreadyArchived.Code, result.Error.Code);
        }
    }
}
