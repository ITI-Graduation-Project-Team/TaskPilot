using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;

namespace TaskPilot.Tests;

public sealed class ProjectSetupStatusTests
{
    [Fact]
    public void GetOverallStatus_ReturnsWarningsForLegacySucceededStateWithPendingTasks()
    {
        var state = ReadyState();
        state.TasksSkipped = 9;

        var status = ProjectSetupService.GetOverallStatus(state);

        Assert.Equal(ProjectSetupOverallStatus.ReadyWithWarnings, status);
    }

    [Fact]
    public void GetOverallStatus_ReturnsReadyWhenEveryTaskHasSkills()
    {
        var status = ProjectSetupService.GetOverallStatus(ReadyState());

        Assert.Equal(ProjectSetupOverallStatus.Ready, status);
    }

    private static ProjectSetupState ReadyState() => new()
    {
        TechStackStatus = TechStackSetupStatus.Confirmed,
        WbsStatus = BackgroundSetupStatus.Succeeded,
        SkillsStatus = BackgroundSetupStatus.Succeeded,
        TasksSkipped = 0
    };
}
