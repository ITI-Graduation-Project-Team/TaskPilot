using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services;

namespace TaskPilot.Tests.Services
{
    public sealed class ProjectSetupStatusTests
    {
        [Fact]
        public void GetOverallStatus_RequiresTechStack_First()
        {
            var state = new ProjectSetupState();

            Assert.Equal(ProjectSetupOverallStatus.NeedsTechStack, ProjectSetupService.GetOverallStatus(state));
        }

        [Fact]
        public void GetOverallStatus_ExposesWbsWhileSkillsContinue()
        {
            var state = new ProjectSetupState
            {
                TechStackStatus = TechStackSetupStatus.Confirmed,
                WbsStatus = BackgroundSetupStatus.Succeeded,
                SkillsStatus = BackgroundSetupStatus.Running
            };

            Assert.Equal(ProjectSetupOverallStatus.EnrichingSkills, ProjectSetupService.GetOverallStatus(state));
        }

        [Fact]
        public void GetOverallStatus_DoesNotBlockBacklogForSkillFailure()
        {
            var state = new ProjectSetupState
            {
                TechStackStatus = TechStackSetupStatus.Confirmed,
                WbsStatus = BackgroundSetupStatus.Succeeded,
                SkillsStatus = BackgroundSetupStatus.Failed
            };

            Assert.Equal(ProjectSetupOverallStatus.ReadyWithWarnings, ProjectSetupService.GetOverallStatus(state));
        }

        [Fact]
        public void GetOverallStatus_ReportsWbsFailure()
        {
            var state = new ProjectSetupState
            {
                TechStackStatus = TechStackSetupStatus.Confirmed,
                WbsStatus = BackgroundSetupStatus.Failed
            };

            Assert.Equal(ProjectSetupOverallStatus.Failed, ProjectSetupService.GetOverallStatus(state));
        }

        [Fact]
        public void ToDto_NormalizesStoredPascalCaseSuggestionToCamelCase()
        {
            var project = new Project { NameEn = "Test" };
            var state = new ProjectSetupState
            {
                TechStackStatus = TechStackSetupStatus.Suggested,
                TechStackSuggestionJson = """
                    {"PrimaryStack":{"Description":"Primary","TechStack":["Angular"],"Reasoning":"Team fit"},"IdealStack":{"Description":"Ideal","TechStack":["Angular","Redis"],"Reasoning":"Best fit"},"GapAnalysis":[],"PlatformTargets":["Web"],"ProjectType":"SaaS"}
                    """
            };

            var dto = ProjectSetupService.ToDto(project, state);
            var suggestion = dto.TechStack.Suggestion!.Value;

            Assert.True(suggestion.TryGetProperty("primaryStack", out var primary));
            Assert.Equal("Team fit", primary.GetProperty("reasoning").GetString());
            Assert.False(suggestion.TryGetProperty("PrimaryStack", out _));
        }
    }
}
