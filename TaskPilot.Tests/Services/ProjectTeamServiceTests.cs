using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.Moq;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class ProjectTeamServiceTests
    {
        private readonly Mock<IRepository<ProjectEmployee>> _projectEmployeeRepoMock;
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IRepository<Employee>> _employeeRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly ProjectTeamService _service;

        public ProjectTeamServiceTests()
        {
            _projectEmployeeRepoMock = new Mock<IRepository<ProjectEmployee>>();
            _projectRepoMock = new Mock<IRepository<Project>>();
            _employeeRepoMock = new Mock<IRepository<Employee>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _notificationServiceMock = new Mock<INotificationService>();

            _service = new ProjectTeamService(
                _projectEmployeeRepoMock.Object,
                _projectRepoMock.Object,
                _employeeRepoMock.Object,
                _unitOfWorkMock.Object,
                _notificationServiceMock.Object
            );
        }

        [Fact]
        public async Task AssignEmployeesAsync_EmployeeInAnotherActiveProject_ReturnsFailure()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();

            var project = new Project { CompanyId = companyId };
            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);

            var employee = new Employee { CompanyId = companyId };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(employee, employeeId);
            _employeeRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<Employee> { employee }.BuildMockDbSet().Object);

            // Mock existing assignment in another project
            var otherProjectId = Guid.NewGuid();
            var otherProject = new Project { Status = ProjectStatus.Active };
            var assignment = new ProjectEmployee
            {
                ProjectId = otherProjectId,
                EmployeeId = employeeId,
                Project = otherProject
            };

            _projectEmployeeRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<ProjectEmployee> { assignment }.BuildMockDbSet().Object);

            var request = new AssignProjectEmployeesRequest
            {
                Assignments = new List<ProjectEmployeeAssignmentDto>
                {
                    new() { EmployeeId = employeeId, Role = ProjectRole.Developer }
                }
            };

            // Act
            var result = await _service.AssignEmployeesAsync(projectId, request);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("EmployeeAlreadyAssignedToAnotherProject", result.Error.Code);
        }

        [Fact]
        public async Task AssignEmployeesAsync_EmployeeInAnotherCompletedProject_ReturnsSuccess()
        {
            // Arrange
            var projectId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();

            var project = new Project { CompanyId = companyId };
            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(project);

            var employee = new Employee { CompanyId = companyId };
            typeof(BaseEntity<Guid>).GetProperty("Id")!.SetValue(employee, employeeId);
            _employeeRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<Employee> { employee }.BuildMockDbSet().Object);

            // Mock assignment in a completed project
            var otherProjectId = Guid.NewGuid();
            var otherProject = new Project { Status = ProjectStatus.Completed };
            var assignment = new ProjectEmployee
            {
                ProjectId = otherProjectId,
                EmployeeId = employeeId,
                Project = otherProject
            };

            _projectEmployeeRepoMock.Setup(r => r.GetQueryable())
                .Returns(new List<ProjectEmployee> { assignment }.BuildMockDbSet().Object);

            var request = new AssignProjectEmployeesRequest
            {
                Assignments = new List<ProjectEmployeeAssignmentDto>
                {
                    new() { EmployeeId = employeeId, Role = ProjectRole.Developer }
                }
            };

            // Act
            var result = await _service.AssignEmployeesAsync(projectId, request);

            // Assert
            Assert.True(result.IsSuccess);
            _projectEmployeeRepoMock.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<ProjectEmployee>>()), Times.Once);
        }
    }
}
