using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Services;
using MockQueryable.Moq;
using TaskPilot.Models.Enums;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class SprintConfirmationServiceTests
    {
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IUserStoryRepository> _userStoryRepoMock;
        private readonly Mock<ITaskRepository> _taskRepoMock;
        private readonly Mock<IRepository<Sprint>> _sprintRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmployeeRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly SprintConfirmationService _service;

        public SprintConfirmationServiceTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _userStoryRepoMock = new Mock<IUserStoryRepository>();
            _taskRepoMock = new Mock<ITaskRepository>();
            _sprintRepoMock = new Mock<IRepository<Sprint>>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _service = new SprintConfirmationService(
                _projectRepoMock.Object,
                _userStoryRepoMock.Object,
                _taskRepoMock.Object,
                _sprintRepoMock.Object,
                _projectEmployeeRepoMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task ConfirmAsync_NoEmployees_ReturnsNoEmployeesAssignedError()
        {
            var projectId = Guid.NewGuid();
            var request = new ConfirmSprintRequest
            {
                TitleEn = "Sprint 1",
                UserStoryIds = new List<Guid> { Guid.NewGuid() }
            };

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId))
                .ReturnsAsync(new Project());

            _projectEmployeeRepoMock.Setup(r => r.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid>());

            var result = await _service.ConfirmAsync(projectId, request);

            Assert.False(result.IsSuccess);
            Assert.Equal(SprintErrors.NoEmployeesAssigned.Code, result.Error!.Code);
        }

        [Fact]
        public async Task ConfirmAsync_HasActiveSprint_ReturnsAnotherSprintAlreadyActiveError()
        {
            var projectId = Guid.NewGuid();
            var request = new ConfirmSprintRequest
            {
                TitleEn = "Sprint 2",
                UserStoryIds = new List<Guid> { Guid.NewGuid() }
            };

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId))
                .ReturnsAsync(new Project());

            _projectEmployeeRepoMock.Setup(r => r.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { Guid.NewGuid() });

            var existingSprints = new List<Sprint>
            {
                new Sprint { ProjectId = projectId, Status = SprintStatus.Active }
            };

            _sprintRepoMock.Setup(r => r.GetQueryable())
                .Returns(existingSprints.BuildMockDbSet().Object);

            var result = await _service.ConfirmAsync(projectId, request);

            Assert.False(result.IsSuccess);
            Assert.Equal(SprintErrors.AnotherSprintAlreadyActive.Code, result.Error!.Code);
        }

        [Fact]
        public async Task ConfirmAsync_HasPlannedSprint_ReturnsAnotherSprintAlreadyPlannedError()
        {
            var projectId = Guid.NewGuid();
            var request = new ConfirmSprintRequest
            {
                TitleEn = "Sprint 2",
                UserStoryIds = new List<Guid> { Guid.NewGuid() }
            };

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId))
                .ReturnsAsync(new Project());

            _projectEmployeeRepoMock.Setup(r => r.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { Guid.NewGuid() });

            var existingSprints = new List<Sprint>
            {
                new Sprint { ProjectId = projectId, Status = SprintStatus.Planned }
            };

            _sprintRepoMock.Setup(r => r.GetQueryable())
                .Returns(existingSprints.BuildMockDbSet().Object);

            var result = await _service.ConfirmAsync(projectId, request);

            Assert.False(result.IsSuccess);
            Assert.Equal(SprintErrors.AnotherSprintAlreadyPlanned.Code, result.Error!.Code);
        }
    }
}
