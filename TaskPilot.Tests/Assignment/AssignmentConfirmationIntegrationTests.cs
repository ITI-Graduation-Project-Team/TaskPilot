using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Assignment;
using TaskPilot.Services.Interfaces;
using Moq;
using Xunit;

namespace TaskPilot.Tests.Assignment
{
    public class AssignmentConfirmationTests
    {
        private readonly Mock<ITaskRepository> _taskRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmpRepoMock;
        private readonly Mock<ILocalizationService> _localizationMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly AssignmentConfirmationService _sut;

        public AssignmentConfirmationTests()
        {
            _taskRepoMock = new Mock<ITaskRepository>();
            _projectEmpRepoMock = new Mock<IProjectEmployeeRepository>();
            _localizationMock = new Mock<ILocalizationService>();
            _notificationMock = new Mock<INotificationService>();

            _localizationMock.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);

            _sut = new AssignmentConfirmationService(
                _taskRepoMock.Object, 
                _projectEmpRepoMock.Object, 
                _localizationMock.Object,
                _notificationMock.Object);
        }

        [Fact]
        public async Task Test_BulkConfirm_15Tasks()
        {
            var projectId = Guid.NewGuid();
            var sprintId = Guid.NewGuid();
            var empId = Guid.NewGuid();

            var tasks = new List<TaskItem>();
            for (int i = 0; i < 15; i++)
            {
                // We must use reflection to set ID since it's inaccessible setter, or we just rely on Moq / test data.
                // However, DTO doesn't require TaskItem to have ID set except to map it. 
                // Wait, if Id cannot be set, we can create a proxy or just use the default Guid?
                // Actually, let's just use reflection to set Id
                var task = new TaskItem { SprintId = sprintId, TitleEn = $"Task {i}", EstimatedHours = 2 };
                typeof(TaskItem).GetProperty("Id")?.SetValue(task, Guid.NewGuid());
                tasks.Add(task);
            }

            _projectEmpRepoMock.Setup(x => x.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { empId });

            _taskRepoMock.Setup(x => x.GetBySprintIdAsync(sprintId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            var request = new ConfirmAssignmentsRequest
            {
                Assignments = tasks.Select(t => new TaskAssignmentDto { TaskId = t.Id, EmployeeId = empId }).ToList()
            };

            var result = await _sut.ConfirmAsync(projectId, sprintId, request, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(15, result.Value.TotalRequested);
            Assert.Equal(15, result.Value.AssignmentsConfirmed);
            Assert.Equal(0, result.Value.OverridesApplied);
            Assert.All(tasks, t => Assert.Equal(empId, t.EmployeeId));
        }

        [Fact]
        public async Task Test_PartialConfirm_5Tasks()
        {
            var projectId = Guid.NewGuid();
            var sprintId = Guid.NewGuid();
            var empId = Guid.NewGuid();

            var tasks = new List<TaskItem>();
            for (int i = 0; i < 15; i++)
            {
                var task = new TaskItem { SprintId = sprintId, TitleEn = $"Task {i}", EstimatedHours = 2 };
                typeof(TaskItem).GetProperty("Id")?.SetValue(task, Guid.NewGuid());
                tasks.Add(task);
            }

            _projectEmpRepoMock.Setup(x => x.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { empId });

            _taskRepoMock.Setup(x => x.GetBySprintIdAsync(sprintId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tasks);

            var request = new ConfirmAssignmentsRequest
            {
                Assignments = tasks.Take(5).Select(t => new TaskAssignmentDto { TaskId = t.Id, EmployeeId = empId }).ToList()
            };

            var result = await _sut.ConfirmAsync(projectId, sprintId, request, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(5, result.Value.AssignmentsConfirmed);
            Assert.Equal(5, tasks.Count(t => t.EmployeeId == empId));
            Assert.Equal(10, tasks.Count(t => t.EmployeeId == null));
        }

        [Fact]
        public async Task Test_Override_TaskAssigned()
        {
            var projectId = Guid.NewGuid();
            var sprintId = Guid.NewGuid();
            var ahmedId = Guid.NewGuid();
            var saraId = Guid.NewGuid();

            var task = new TaskItem { SprintId = sprintId, TitleEn = "Task 1", EmployeeId = ahmedId, EstimatedHours = 2 };
            typeof(TaskItem).GetProperty("Id")?.SetValue(task, Guid.NewGuid());

            _projectEmpRepoMock.Setup(x => x.GetEmployeeIdsByProjectAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid> { ahmedId, saraId });

            _taskRepoMock.Setup(x => x.GetBySprintIdAsync(sprintId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TaskItem> { task });

            var request = new ConfirmAssignmentsRequest
            {
                Assignments = new List<TaskAssignmentDto> { new TaskAssignmentDto { TaskId = task.Id, EmployeeId = saraId } }
            };

            var result = await _sut.ConfirmAsync(projectId, sprintId, request, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.AssignmentsConfirmed);
            Assert.Equal(1, result.Value.OverridesApplied);
            Assert.Equal(saraId, task.EmployeeId);
        }
    }
}
