using TaskPilot.AI.Models.Requirements;

namespace TaskPilot.Tests.AI;

public class RequirementCompletenessReportTests
{
    [Theory]
    [InlineData(84, false)]
    [InlineData(85, true)]
    [InlineData(88, true)]
    [InlineData(100, true)]
    public void MeetsConfirmationThreshold_MatchesLegacyUiPolicy(int score, bool expected)
    {
        var report = new RequirementCompletenessReport
        {
            OverallCompleteness = score
        };

        Assert.Equal(expected, report.MeetsConfirmationThreshold());
    }
}
