using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.DTOs.Planning;

namespace TaskPilot.Tests.AI
{
    public class SprintSuggestionTests
    {
        [Fact]
        public void TryRepairJson_WithTruncatedSprintSuggestion_SuccessfullyRepairs()
        {
            // Arrange
            // Simulating a real truncation scenario mid-array where the AI cuts off while generating a story
            string truncatedJson = @"{
  ""sprintNumber"": 1,
  ""sprintTitleEn"": ""Sprint 1: Core Features"",
  ""sprintTitleAr"": ""سبرينت 1"",
  ""sprintGoalEn"": ""Deliver core functionality."",
  ""sprintGoalAr"": ""تسليم الوظائف الأساسية"",
  ""totalEstimatedHours"": 40,
  ""risks"": [""Risk 1""],
  ""stories"": [
    {
      ""storyId"": ""a0000000-0000-0000-0000-000000000000"",
      ""titleEn"": ""Story 1"",
      ""titleAr"": ""قصة 1"",
      ""estimatedHours"": 10,
      ""priorityScore"": 100,
      ""reasonEn"": ""Because."",
      ""reasonAr"": ""لأن.""
    },
    {
      ""storyId"": ""b0000000-0000-0000-0000-000000000000"",
      ""titleEn"": ""Story 2"",
      ""titleAr"": ""قصة 2"",
      ""estimatedHours"": 5,
      ""priorityScore"": 90,
      ""reasonEn"": ""Because."",
      ""reasonAr"": ""لأن."""; // TRUNCATED MID-STORY 2

            // Act
            var repaired = SprintSuggestionAgent.TryRepairJson(truncatedJson);

            // Assert
            var dto = JsonSerializer.Deserialize<SprintSuggestionDto>(repaired, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            Assert.NotNull(dto);
            Assert.Equal(1, dto.SprintNumber);
            Assert.Single(dto.Stories); // The first fully completed story should remain, the partial one is truncated out
            Assert.Equal("a0000000-0000-0000-0000-000000000000", dto.Stories[0].StoryId.ToString());
        }
    }
}
