using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MockQueryable.Moq;
using Moq;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Skills;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Services;
using TaskPilot.Models.Enums;
using Xunit;

namespace TaskPilot.Tests.Skills;

public class SkillMigrationServiceTests
{
    private readonly Mock<IRepository<Skill>> _skillRepoMock;
    private readonly Mock<IRepository<SkillAlias>> _skillAliasRepoMock;
    private readonly Mock<IRepository<UserSkill>> _userSkillRepoMock;
    private readonly Mock<IRepository<TaskRequiredSkill>> _taskRequiredSkillRepoMock;
    private readonly SkillMigrationService _sut;

    public SkillMigrationServiceTests()
    {
        _skillRepoMock = new Mock<IRepository<Skill>>();
        _skillAliasRepoMock = new Mock<IRepository<SkillAlias>>();
        _userSkillRepoMock = new Mock<IRepository<UserSkill>>();
        _taskRequiredSkillRepoMock = new Mock<IRepository<TaskRequiredSkill>>();

        _sut = new SkillMigrationService(
            _skillRepoMock.Object,
            _skillAliasRepoMock.Object,
            _userSkillRepoMock.Object,
            _taskRequiredSkillRepoMock.Object);
    }

    private T SetId<T, TId>(T entity, TId id) where T : TaskPilot.Models.Common.BaseEntity<TId>
    {
        var prop = typeof(TaskPilot.Models.Common.BaseEntity<TId>).GetProperty("Id");
        prop!.SetValue(entity, id);
        return entity;
    }

    [Fact]
    public async Task MergeSkillsAsync_EmptyObsoleteList_ReturnsEmptyListError()
    {
        var request = new SkillMergeRequestDto { CanonicalSkillId = 1, ObsoleteSkillIds = new List<int>() };
        var result = await _sut.MergeSkillsAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(SkillErrors.EmptyList.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MergeSkillsAsync_CanonicalInObsoleteList_ReturnsDuplicateError()
    {
        var request = new SkillMergeRequestDto { CanonicalSkillId = 1, ObsoleteSkillIds = new List<int> { 1, 2 } };
        var result = await _sut.MergeSkillsAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(SkillErrors.DuplicateCanonicalSkill.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MergeSkillsAsync_CanonicalSkillNotFound_ReturnsNotFound()
    {
        var request = new SkillMergeRequestDto { CanonicalSkillId = 99, ObsoleteSkillIds = new List<int> { 2 } };
        
        var skills = new List<Skill>().BuildMockDbSet();
        _skillRepoMock.Setup(r => r.GetQueryable()).Returns(skills.Object);

        var result = await _sut.MergeSkillsAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(SkillErrors.CanonicalSkillNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task MergeSkillsAsync_ValidRequest_MigratesAndCreatesAliases()
    {
        var canonical = SetId(new Skill { Name = "ASP.NET Core" }, 1);
        var obsolete = SetId(new Skill { Name = "ASP.NET Web API" }, 2);
        var skills = new List<Skill> { canonical, obsolete }.BuildMockDbSet();
        _skillRepoMock.Setup(r => r.GetQueryable()).Returns(skills.Object);

        var aliases = new List<SkillAlias>().BuildMockDbSet();
        _skillAliasRepoMock.Setup(r => r.GetQueryable()).Returns(aliases.Object);

        var userSkills = new List<UserSkill>
        {
            SetId(new UserSkill { UserId = Guid.NewGuid(), SkillId = 2, Level = SkillLevel.Beginner }, Guid.NewGuid())
        }.BuildMockDbSet();
        _userSkillRepoMock.Setup(r => r.GetQueryable()).Returns(userSkills.Object);

        var taskSkills = new List<TaskRequiredSkill>().BuildMockDbSet();
        _taskRequiredSkillRepoMock.Setup(r => r.GetQueryable()).Returns(taskSkills.Object);

        var request = new SkillMergeRequestDto { CanonicalSkillId = 1, ObsoleteSkillIds = new List<int> { 2 } };
        var result = await _sut.MergeSkillsAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.AliasesCreated);
        Assert.Equal(1, result.Value!.EmployeeSkillsMigrated);
        Assert.Equal(0, result.Value!.TaskRequiredSkillsMigrated);
        Assert.Equal(1, result.Value!.ObsoleteSkillsProcessed);
        Assert.True(obsolete.IsDeleted);

        _skillAliasRepoMock.Verify(r => r.AddAsync(It.Is<SkillAlias>(a => a.Alias == "ASP.NET Web API" && a.SkillId == 1)), Times.Once);
        _userSkillRepoMock.Verify(r => r.Update(It.Is<UserSkill>(us => us.SkillId == 1)), Times.Once);
        _skillRepoMock.Verify(r => r.Update(It.Is<Skill>(s => s.Id == 2 && s.IsDeleted)), Times.Once);
    }

    [Fact]
    public async Task MergeSkillsAsync_AliasAlreadyExists_ReturnsConflict()
    {
        var canonical = SetId(new Skill { Name = "HTML/CSS" }, 1);
        var obsolete = SetId(new Skill { Name = "HTML5" }, 2);
        var skills = new List<Skill> { canonical, obsolete }.BuildMockDbSet();
        _skillRepoMock.Setup(r => r.GetQueryable()).Returns(skills.Object);

        // Alias "HTML5" already exists globally
        var aliases = new List<SkillAlias>
        {
            new SkillAlias { SkillId = 3, Alias = "HTML5" }
        };
        // wait, SkillAlias doesn't inherit from BaseEntity<int> if Id is not protected.
        // Let's set Id manually if it allows.
        aliases[0].Id = 1;
        
        var mockAliases = aliases.BuildMockDbSet();
        _skillAliasRepoMock.Setup(r => r.GetQueryable()).Returns(mockAliases.Object);

        var request = new SkillMergeRequestDto { CanonicalSkillId = 1, ObsoleteSkillIds = new List<int> { 2 } };
        var result = await _sut.MergeSkillsAsync(request);

        Assert.True(result.IsFailure);
        Assert.Equal(SkillErrors.AliasAlreadyExists.Code, result.Error!.Code);
    }
}
