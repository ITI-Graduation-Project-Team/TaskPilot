using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;
using Xunit;

namespace TaskPilot.Tests.Planning
{
    public class WbsSkillEnrichmentServiceTests
    {
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IRepository<TaskItem>> _taskRepoMock;
        private readonly Mock<IRepository<Skill>> _skillRepoMock;
        private readonly Mock<IRepository<TaskRequiredSkill>> _taskRequiredSkillRepoMock;
        private readonly Mock<IAiKernelService> _kernelServiceMock;
        private readonly Mock<IPromptLoaderService> _promptLoaderMock;
        private readonly Mock<ILogger<RequiredSkillsEnrichmentAgent>> _loggerMock;
        private readonly RequiredSkillsEnrichmentAgent _agent;
        private readonly WbsSkillEnrichmentService _sut;

        public WbsSkillEnrichmentServiceTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _taskRepoMock = new Mock<IRepository<TaskItem>>();
            _skillRepoMock = new Mock<IRepository<Skill>>();
            _taskRequiredSkillRepoMock = new Mock<IRepository<TaskRequiredSkill>>();

            _kernelServiceMock = new Mock<IAiKernelService>();
            _promptLoaderMock = new Mock<IPromptLoaderService>();
            _loggerMock = new Mock<ILogger<RequiredSkillsEnrichmentAgent>>();

            _agent = new RequiredSkillsEnrichmentAgent(
                _kernelServiceMock.Object,
                _promptLoaderMock.Object,
                _loggerMock.Object);

            _sut = new WbsSkillEnrichmentService(
                _projectRepoMock.Object,
                _taskRepoMock.Object,
                _skillRepoMock.Object,
                _taskRequiredSkillRepoMock.Object,
                _agent);
        }

        [Fact]
        public async Task EnrichProjectTasksAsync_ProjectNotFound_ReturnsNotFound()
        {
            _projectRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Project)null);
            var result = await _sut.EnrichProjectTasksAsync(Guid.NewGuid());
            Assert.False(result.IsSuccess);
            Assert.Equal(WbsErrors.ProjectNotFound.Code, result.Error.Code);
        }

        [Fact]
        public async Task EnrichProjectTasksAsync_NoTasksAvailable_ReturnsSuccessWithZeroEnriched()
        {
            _projectRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Project());

            var emptyTasks = new List<TaskItem>();
            _taskRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TaskItem, bool>>>()))
                .ReturnsAsync(emptyTasks);

            var result = await _sut.EnrichProjectTasksAsync(Guid.NewGuid());

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value.TasksProcessed);
        }
    }
}
