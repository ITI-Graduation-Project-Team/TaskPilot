using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;
using TaskPilot.Services;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Tests;

public class ProjectDuplicateNameTests
{
    [Fact]
    public async Task CreateAsync_ReturnsConflict_WhenNormalizedNameAlreadyExists()
    {
        var projectRepo = new Mock<IRepository<Project>>();
        projectRepo
            .Setup(repository => repository.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>()))
            .ReturnsAsync(true);

        var service = CreateService(projectRepo, managerExists: true, companyExists: true);

        var result = await service.CreateAsync(new CreateProjectDto
        {
            CompanyId = Guid.NewGuid(),
            NameEn = "  TASKPILOT  ",
            NameAr = "تاسك بايلوت",
        });

        Assert.True(result.IsFailure);
        Assert.Equal("PROJECT_NAME_ALREADY_EXISTS", result.Error.Code);
        projectRepo.Verify(repository => repository.AddAsync(It.IsAny<Project>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_TrimsEnglishName_WhenNameIsAvailable()
    {
        var projectRepo = new Mock<IRepository<Project>>();
        projectRepo
            .Setup(repository => repository.AnyAsync(It.IsAny<Expression<Func<Project, bool>>>()))
            .ReturnsAsync(false);
        projectRepo
            .Setup(repository => repository.AddAsync(It.IsAny<Project>()))
            .Returns(Task.CompletedTask);

        var service = CreateService(projectRepo, managerExists: true, companyExists: true);

        var result = await service.CreateAsync(new CreateProjectDto
        {
            CompanyId = Guid.NewGuid(),
            NameEn = "  TaskPilot  ",
            NameAr = "تاسك بايلوت",
        });

        Assert.True(result.IsSuccess);
        projectRepo.Verify(repository => repository.AddAsync(
            It.Is<Project>(project => project.NameEn == "TaskPilot")), Times.Once);
    }

    [Fact]
    public void DuplicateDetector_RecognizesProjectNameIndex()
    {
        var exception = new DbUpdateException(
            "Save failed",
            new Exception("Violation of UNIQUE KEY constraint 'IX_Projects_CompanyId_NormalizedNameEn'."));

        Assert.True(ProjectDuplicateNameDetector.IsDuplicateNameViolation(exception));
    }

    private static ProjectService CreateService(
        Mock<IRepository<Project>> projectRepo,
        bool managerExists,
        bool companyExists)
    {
        var companyRepo = new Mock<IRepository<Company>>();
        companyRepo
            .Setup(repository => repository.AnyAsync(It.IsAny<Expression<Func<Company, bool>>>()))
            .ReturnsAsync(companyExists);

        var managerRepo = new Mock<IRepository<ProjectManager>>();
        managerRepo
            .Setup(repository => repository.AnyAsync(It.IsAny<Expression<Func<ProjectManager, bool>>>()))
            .ReturnsAsync(managerExists);

        var localization = new Mock<ILocalizationService>();
        localization.SetupGet(service => service.CurrentLanguage).Returns("en");

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(Guid.NewGuid());

        return new ProjectService(
            projectRepo.Object,
            companyRepo.Object,
            managerRepo.Object,
            localization.Object,
            Mock.Of<ILogger<ProjectService>>(),
            currentUser.Object);
    }
}
