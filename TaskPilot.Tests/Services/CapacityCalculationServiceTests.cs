using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.Moq;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Implementations;
using Xunit;

namespace TaskPilot.Tests.Services
{
    public class CapacityCalculationServiceTests
    {
        private readonly Mock<IRepository<Project>> _projectRepoMock;
        private readonly Mock<IRepository<Company>> _companyRepoMock;
        private readonly Mock<IProjectEmployeeRepository> _projectEmployeeRepoMock;
        private readonly CapacityCalculationService _service;

        public CapacityCalculationServiceTests()
        {
            _projectRepoMock = new Mock<IRepository<Project>>();
            _companyRepoMock = new Mock<IRepository<Company>>();
            _projectEmployeeRepoMock = new Mock<IProjectEmployeeRepository>();

            _service = new CapacityCalculationService(
                _projectRepoMock.Object,
                _companyRepoMock.Object,
                _projectEmployeeRepoMock.Object);
        }

        private Guid SetupMocks(
            decimal hoursPerDay, 
            int workingDaysMask, 
            decimal buffer, 
            List<ProjectEmployee> employees)
        {
            var companyId = Guid.NewGuid();
            var projectId = Guid.NewGuid();

            var project = new Project { CompanyId = companyId };
            project.GetType().GetProperty("Id")!.SetValue(project, projectId);

            var company = new Company 
            { 
                WorkingHoursPerDay = hoursPerDay,
                WorkingDaysMask = workingDaysMask,
                DefaultCapacityBufferPercentage = buffer
            };
            company.GetType().GetProperty("Id")!.SetValue(company, companyId);

            _projectRepoMock.Setup(r => r.GetByIdAsync(projectId))
                .ReturnsAsync(project);

            _companyRepoMock.Setup(r => r.GetByIdAsync(companyId))
                .ReturnsAsync(company);

            foreach (var emp in employees)
            {
                emp.ProjectId = projectId;
            }

            _projectEmployeeRepoMock.Setup(r => r.GetActiveByProjectIdAsync(projectId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(employees.Where(e => e.IsActive).ToList());

            return projectId;
        }

        [Fact]
        public async Task CalculateTargetSprintHoursAsync_1EmployeeAt100Percent()
        {
            var employees = new List<ProjectEmployee>
            {
                new ProjectEmployee { IsActive = true, AllocationPercentage = 100m }
            };

            var projectId = SetupMocks(8.0m, 62, 0.8m, employees);

            var start = new DateTime(2023, 10, 2); // Monday
            var end = new DateTime(2023, 10, 15);  // Sunday (14 days, 10 working days)

            var result = await _service.CalculateTargetSprintHoursAsync(projectId, start, end);

            Assert.True(result.IsSuccess);
            Assert.Equal(64m, result.Value!.TargetSprintHours);
        }

        [Fact]
        public async Task CalculateTargetSprintHoursAsync_2EmployeesAt100Percent()
        {
            var employees = new List<ProjectEmployee>
            {
                new ProjectEmployee { IsActive = true, AllocationPercentage = 100m },
                new ProjectEmployee { IsActive = true, AllocationPercentage = 100m }
            };

            var projectId = SetupMocks(8.0m, 62, 0.8m, employees);

            var start = new DateTime(2023, 10, 2); // Monday
            var end = new DateTime(2023, 10, 15);  // Sunday

            var result = await _service.CalculateTargetSprintHoursAsync(projectId, start, end);

            Assert.True(result.IsSuccess);
            Assert.Equal(128m, result.Value!.TargetSprintHours);
        }

        [Fact]
        public async Task CalculateTargetSprintHoursAsync_2EmployeesOneAt50Percent()
        {
            var employees = new List<ProjectEmployee>
            {
                new ProjectEmployee { IsActive = true, AllocationPercentage = 100m },
                new ProjectEmployee { IsActive = true, AllocationPercentage = 50m }
            };

            var projectId = SetupMocks(8.0m, 62, 0.8m, employees);

            var start = new DateTime(2023, 10, 2); // Monday
            var end = new DateTime(2023, 10, 15);  // Sunday

            var result = await _service.CalculateTargetSprintHoursAsync(projectId, start, end);

            Assert.True(result.IsSuccess);
            Assert.Equal(96m, result.Value!.TargetSprintHours);
        }

        [Fact]
        public async Task CalculateTargetSprintHoursAsync_SunThu_vs_MonFri()
        {
            var employees = new List<ProjectEmployee>
            {
                new ProjectEmployee { IsActive = true, AllocationPercentage = 100m }
            };

            // Sun-Thu mask = 1 + 2 + 4 + 8 + 16 = 31
            var projectId = SetupMocks(8.0m, 31, 1.0m, employees);

            var start = new DateTime(2023, 10, 6); // Friday
            var end = new DateTime(2023, 10, 8);   // Sunday
            
            // For Sun-Thu mask, Friday and Saturday are off. Sunday is working day.
            // 3 days total, 1 working day (Sunday)
            // 1 * 8 * 1.0 = 8

            var resultSunThu = await _service.CalculateTargetSprintHoursAsync(projectId, start, end);

            Assert.True(resultSunThu.IsSuccess);
            Assert.Equal(8m, resultSunThu.Value!.TargetSprintHours);

            // Mon-Fri mask = 62
            projectId = SetupMocks(8.0m, 62, 1.0m, employees);

            // For Mon-Fri mask, Friday is working day. Saturday, Sunday are off.
            // 3 days total, 1 working day (Friday)
            var resultMonFri = await _service.CalculateTargetSprintHoursAsync(projectId, start, end);
            
            Assert.True(resultMonFri.IsSuccess);
            Assert.Equal(8m, resultMonFri.Value!.TargetSprintHours);
        }
        
        [Fact]
        public async Task CalculateTargetSprintHoursAsync_NoActiveEmployees_ReturnsZero()
        {
            var employees = new List<ProjectEmployee>
            {
                new ProjectEmployee { IsActive = false, AllocationPercentage = 100m }
            };

            var projectId = SetupMocks(8.0m, 62, 0.8m, employees);

            var start = new DateTime(2023, 10, 2);
            var end = new DateTime(2023, 10, 15);

            var result = await _service.CalculateTargetSprintHoursAsync(projectId, start, end);

            Assert.True(result.IsSuccess);
            Assert.Equal(0m, result.Value!.TargetSprintHours);
        }
    }
}
