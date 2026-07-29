using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Services;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class SprintPlanningServiceTests
    {
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IUserStoryRepository> _userStoryRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmployeeRepoMock;
        private readonly Mock<IRepository<SprintRetrospective>> _retrospectiveRepoMock;
        private readonly Mock<SprintSuggestionAgent> _agentMock;
        private readonly Mock<ILogger<SprintPlanningService>> _loggerMock;
        private readonly SprintPlanningService _service;

        private readonly Mock<IRepository<Sprint>> _sprintRepoMock;

        public SprintPlanningServiceTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _userStoryRepoMock = new Mock<IUserStoryRepository>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();
            _retrospectiveRepoMock = new Mock<IRepository<SprintRetrospective>>();
            _sprintRepoMock = new Mock<IRepository<Sprint>>();
            _loggerMock = new Mock<ILogger<SprintPlanningService>>();
            _agentMock = new Mock<SprintSuggestionAgent>(null!, null!);

            _retrospectiveRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<SprintRetrospective>().AsQueryable());
            _sprintRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<Sprint>().AsQueryable());

            _service = new SprintPlanningService(
                _projectRepoMock.Object,
                _userStoryRepoMock.Object,
                _projectEmployeeRepoMock.Object,
                _retrospectiveRepoMock.Object,
                _sprintRepoMock.Object,
                null!,
                _agentMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task GenerateSprintSuggestionAsync_NoEmployees_ReturnsNoEmployeesAssignedError()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { NameEn = "Test Project" };

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId))
                .ReturnsAsync(project);

            _projectEmployeeRepoMock.Setup(r => r.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid>());

            var result = await _service.GenerateSprintSuggestionAsync(projectId);

            Assert.False(result.IsSuccess);
            Assert.Equal(SprintErrors.NoEmployeesAssigned.Code, result.Error!.Code);
        }

        [Fact]
        public async Task GenerateSprintSuggestionAsync_HasEmployeesNoBacklog_ReturnsNoBacklogError()
        {
            var projectId = Guid.NewGuid();
            var project = new Project { NameEn = "Test Project" };

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId))
                .ReturnsAsync(project);

            _projectEmployeeRepoMock.Setup(r => r.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { Guid.NewGuid() });

            _userStoryRepoMock.Setup(r => r.GetByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UserStory>());

            var result = await _service.GenerateSprintSuggestionAsync(projectId);

            Assert.False(result.IsSuccess);
            Assert.Equal("INVALID_INPUT", result.Error!.Code);
        }
    }
}
