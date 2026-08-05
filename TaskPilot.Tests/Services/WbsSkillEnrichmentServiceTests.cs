using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class WbsSkillEnrichmentServiceTests
    {
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IRepository<TaskItem>> _taskRepoMock;
        private readonly Mock<IRepository<Skill>> _skillRepoMock;
        private readonly Mock<IRepository<TaskRequiredSkill>> _taskRequiredSkillRepoMock;
        private readonly Mock<RequiredSkillsEnrichmentAgent> _agentMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        public WbsSkillEnrichmentServiceTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _taskRepoMock = new Mock<IRepository<TaskItem>>();
            _skillRepoMock = new Mock<IRepository<Skill>>();
            _taskRequiredSkillRepoMock = new Mock<IRepository<TaskRequiredSkill>>();
            
            // RequiredSkillsEnrichmentAgent dependencies
            var kernelServiceMock = new Mock<TaskPilot.AI.Services.Interfaces.IAiKernelService>();
            var promptLoaderMock = new Mock<TaskPilot.AI.Services.Interfaces.IPromptLoaderService>();
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<RequiredSkillsEnrichmentAgent>>();
            _agentMock = new Mock<RequiredSkillsEnrichmentAgent>(kernelServiceMock.Object, promptLoaderMock.Object, loggerMock.Object, null!);

            _unitOfWorkMock = new Mock<IUnitOfWork>();
        }

        private WbsSkillEnrichmentService CreateService()
        {
            return new WbsSkillEnrichmentService(
                _projectRepoMock.Object,
                _taskRepoMock.Object,
                _skillRepoMock.Object,
                _taskRequiredSkillRepoMock.Object,
                _agentMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task EnrichProjectTasksAsync_WithInBatchDuplicate_OnlyCreatesOneSkill()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var service = CreateService();

            _projectRepoMock.Setup(x => x.GetByIdAsync(projectId)).ReturnsAsync(new Project());

            var task1 = new TaskItem { TitleEn = "Task 1", Type = TaskType.Technical, UserStory = new UserStory() };
            var task2 = new TaskItem { TitleEn = "Task 2", Type = TaskType.Technical, UserStory = new UserStory() };
            task1.GetType().GetProperty("Id").SetValue(task1, Guid.NewGuid());
            task2.GetType().GetProperty("Id").SetValue(task2, Guid.NewGuid());
            
            var tasks = new List<TaskItem> { task1, task2 };

            _taskRepoMock.Setup(x => x.FindAsync(It.IsAny<Expression<Func<TaskItem, bool>>>()))
                .ReturnsAsync(tasks);

            _skillRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Skill>());

            // Return "Kubernetes" for both tasks
            _agentMock.Setup(x => x.EnrichAsync("Task 1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(new List<TaskPilot.AI.Models.Planning.GeneratedRequiredSkill>
                {
                    new TaskPilot.AI.Models.Planning.GeneratedRequiredSkill { SkillName = "Kubernetes", RequiredLevel = "Intermediate" }
                }));

            _agentMock.Setup(x => x.EnrichAsync("Task 2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(new List<TaskPilot.AI.Models.Planning.GeneratedRequiredSkill>
                {
                    new TaskPilot.AI.Models.Planning.GeneratedRequiredSkill { SkillName = "Kubernetes", RequiredLevel = "Beginner" }
                }));

            // Simulate the DB find for Kubernetes: null first time
            _skillRepoMock.Setup(x => x.FindSingleAsync(It.IsAny<Expression<Func<Skill, bool>>>()))
                .ReturnsAsync((Skill)null);

            // Act
            var result = await service.EnrichProjectTasksAsync(projectId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.SkillsCreated);
            Assert.Equal(2, result.Value.TasksEnriched);

            _skillRepoMock.Verify(x => x.AddAsync(It.IsAny<Skill>()), Times.Once);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            
            // Check that TaskRequiredSkill was added for both
            _taskRequiredSkillRepoMock.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<TaskRequiredSkill>>(skills => skills.Count() == 2)), Times.Once);
        }

        [Fact]
        public async Task EnrichProjectTasksAsync_WithExistingDatabaseSkill_ReusesSkill()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var service = CreateService();

            _projectRepoMock.Setup(x => x.GetByIdAsync(projectId)).ReturnsAsync(new Project());

            var task1 = new TaskItem { TitleEn = "Task 1", Type = TaskType.Technical, UserStory = new UserStory() };
            task1.GetType().GetProperty("Id").SetValue(task1, Guid.NewGuid());
            
            var tasks = new List<TaskItem> { task1 };

            _taskRepoMock.Setup(x => x.FindAsync(It.IsAny<Expression<Func<TaskItem, bool>>>()))
                .ReturnsAsync(tasks);

            _skillRepoMock.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<Skill>());

            _agentMock.Setup(x => x.EnrichAsync("Task 1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success(new List<TaskPilot.AI.Models.Planning.GeneratedRequiredSkill>
                {
                    new TaskPilot.AI.Models.Planning.GeneratedRequiredSkill { SkillName = "Kubernetes", RequiredLevel = "Intermediate" }
                }));

            var existingSkill = new Skill { Name = "Kubernetes", NormalizedName = "kubernetes" };
            existingSkill.GetType().GetProperty("Id").SetValue(existingSkill, 1);
            
            _skillRepoMock.Setup(x => x.FindSingleAsync(It.IsAny<Expression<Func<Skill, bool>>>()))
                .ReturnsAsync(existingSkill);

            // Act
            var result = await service.EnrichProjectTasksAsync(projectId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.Value.SkillsCreated);
            Assert.Equal(1, result.Value.TasksEnriched);

            _skillRepoMock.Verify(x => x.AddAsync(It.IsAny<Skill>()), Times.Never);
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            
            _taskRequiredSkillRepoMock.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<TaskRequiredSkill>>(skills => skills.First().Skill.Name == "Kubernetes")), Times.Once);
        }
    }
}
