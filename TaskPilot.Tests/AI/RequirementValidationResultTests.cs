using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Extensions;

namespace TaskPilot.Tests.AI;

public class RequirementValidationResultTests
{
    [Fact]
    public void Parser_AcceptsMarkdownFencedValidationJson()
    {
        const string response = """
            ```json
            {"ValidationScore":85,"Issues":[],"Warnings":[],"BusinessReadiness":"Ready"}
            ```
            """;

        var result = AiResponseParser.Parse<RequirementValidationResult>(response);

        Assert.NotNull(result);
        Assert.Equal(85, result.ValidationScore);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void HasBlockingIssues_LowScoreWithoutCriticalIssues_DoesNotBlock()
    {
        var result = new RequirementValidationResult
        {
            ValidationScore = 70,
            Warnings = ["A non-critical requirement could be clearer."]
        };

        Assert.False(result.HasBlockingIssues(80));
    }

    [Fact]
    public void HasBlockingIssues_LowScoreWithCriticalIssue_Blocks()
    {
        var result = new RequirementValidationResult
        {
            ValidationScore = 70,
            Issues = ["Two requirements contradict each other."]
        };

        Assert.True(result.HasBlockingIssues(80));
    }

    [Fact]
    public void HasBlockingIssues_PassingScore_DoesNotBlock()
    {
        var result = new RequirementValidationResult
        {
            ValidationScore = 90,
            Issues = ["An issue was reported but the configured score passed."]
        };

        Assert.False(result.HasBlockingIssues(80));
    }
}
