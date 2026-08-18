using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;

namespace TaskPilot.Tests;

public sealed class RequiredSkillsEnrichmentAgentTests
{
    [Fact]
    public void ValidateSkills_DropsInvalidValuesAndDeduplicatesNames()
    {
        var result = RequiredSkillsEnrichmentAgent.ValidateSkills(new List<GeneratedRequiredSkill>
        {
            new() { SkillName = " C# ", RequiredLevel = "Intermediate" },
            new() { SkillName = "c#", RequiredLevel = "Advanced" },
            new() { SkillName = "", RequiredLevel = "Beginner" },
            new() { SkillName = "React", RequiredLevel = "Unknown" }
        });

        var skill = Assert.Single(result);
        Assert.Equal("C#", skill.SkillName);
        Assert.Equal("Intermediate", skill.RequiredLevel);
    }

    [Fact]
    public void ValidateSkills_ReturnsEmptyWhenTheAiProducedNoUsableSkills()
    {
        var result = RequiredSkillsEnrichmentAgent.ValidateSkills(new List<GeneratedRequiredSkill>
        {
            new() { SkillName = "", RequiredLevel = "Unknown" }
        });

        Assert.Empty(result);
    }
}
