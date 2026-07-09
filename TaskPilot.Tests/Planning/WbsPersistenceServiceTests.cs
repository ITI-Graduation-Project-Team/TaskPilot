using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;
using Xunit;

namespace TaskPilot.Tests.Planning
{
    public class WbsPersistenceServiceTests
    {
        private readonly Mock<IUserStoryRepository> _userStoryRepoMock;
        private readonly Mock<ITaskRepository> _taskRepoMock;
        private readonly WbsPersistenceService _sut;

        public WbsPersistenceServiceTests()
        {
            _userStoryRepoMock = new Mock<IUserStoryRepository>();
            _taskRepoMock = new Mock<ITaskRepository>();

            _sut = new WbsPersistenceService(
                _userStoryRepoMock.Object,
                _taskRepoMock.Object);
        }

        [Fact]
        public async Task PersistAsync_ValidWbs_CallsServiceAndPersists()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var wbs = new GeneratedWbs
            {
                UserStories = new List<GeneratedUserStory>
                {
                    new GeneratedUserStory
                    {
                        TitleEn = "Story 1",
                        Priority = "High",
                        Tasks = new List<GeneratedTask>
                        {
                            new GeneratedTask
                            {
                                TitleEn = "Task 1",
                                EstimatedHours = 5
                            }
                        }
                    }
                }
            };

            // Act
            var result = await _sut.PersistAsync(projectId, wbs, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.UserStoriesCreated);
            Assert.Equal(1, result.Value.TasksCreated);

            _userStoryRepoMock.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<UserStory>>(s => s.Count() == 1)), Times.Once);
            _taskRepoMock.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<TaskItem>>(t => t.Count() == 1)), Times.Once);
        }

    }
}
