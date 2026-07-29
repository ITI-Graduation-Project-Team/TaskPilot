using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Sprints;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Services;
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
        private readonly Mock<IBackgroundJobClient> _jobClientMock;
        private readonly SprintConfirmationService _service;

        public SprintConfirmationServiceTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _userStoryRepoMock = new Mock<IUserStoryRepository>();
            _taskRepoMock = new Mock<ITaskRepository>();
            _sprintRepoMock = new Mock<IRepository<Sprint>>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _jobClientMock = new Mock<IBackgroundJobClient>();

            _service = new SprintConfirmationService(
                _projectRepoMock.Object,
                _userStoryRepoMock.Object,
                _taskRepoMock.Object,
                _sprintRepoMock.Object,
                _projectEmployeeRepoMock.Object,
                _unitOfWorkMock.Object,
                _jobClientMock.Object);
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
    }
}
