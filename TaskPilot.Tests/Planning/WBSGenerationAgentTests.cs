using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Exceptions;
using TaskPilot.AI.Models.Planning;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Entities;
using Xunit;

namespace TaskPilot.Tests.Planning
{
    public class WBSGenerationAgentTests
    {
        private class MockChatCompletionService : IChatCompletionService
        {
            public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

            public Queue<string> Responses { get; set; } = new Queue<string>();
            public List<string> PromptsReceived { get; set; } = new List<string>();

            public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
            {
                PromptsReceived.Add(chatHistory.Last().Content ?? string.Empty);
                var responseText = Responses.Any() ? Responses.Dequeue() : "{}";
                var result = new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, responseText) };
                return Task.FromResult<IReadOnlyList<ChatMessageContent>>(result);
            }

            public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public async Task GenerateAsync_RetryBehavior_ReducesPromptOnSubsequentAttempts()
        {
            // Arrange
            var mockKernelService = new Mock<IAiKernelService>();
            var validYaml = System.IO.File.ReadAllText(@"..\..\..\..\TaskPilot.AI\Prompts\Planning\WbsGeneration.yaml");
            var mockPromptLoader = new Mock<IPromptLoaderService>();
            mockPromptLoader.Setup(p => p.LoadAsync(It.IsAny<string>())).ReturnsAsync(validYaml);

            var mockChatService = new MockChatCompletionService();
            // Force 2 failures (invalid json) then 1 success
            mockChatService.Responses.Enqueue("invalid json");
            mockChatService.Responses.Enqueue("{ truncated json");
            mockChatService.Responses.Enqueue("{\"userStories\": [{\"titleEn\": \"Story 1\", \"tasks\": []}]}");

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IChatCompletionService>(mockChatService);
            var kernel = builder.Build();

            mockKernelService.Setup(k => k.CreateKernel(It.IsAny<string>())).Returns(kernel);

            var agent = new WBSGenerationAgent(mockKernelService.Object, mockPromptLoader.Object);

            var snapshot = new RequirementsSnapshot { BusinessRequirements = new List<string> { "Req 1" } };

            // Act
            var result = await agent.GenerateAsync(snapshot, new List<string>(), new List<string>(), "General", new List<string>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.UserStories);
            Assert.Equal(3, mockChatService.PromptsReceived.Count);
            
            // First attempt uses original prompt
            Assert.Contains("You are a Senior Agile Solution Architect", mockChatService.PromptsReceived[0]);
            Assert.DoesNotContain("Prioritize JSON completeness over quantity.", mockChatService.PromptsReceived[0]);
            
            // Second attempt adds reduction text
            Assert.Contains("Reduce the number of User Stories", mockChatService.PromptsReceived[1]);
            
            // Third attempt adds extreme reduction text
            Assert.Contains("Generate between 3 and 5 User Stories only", mockChatService.PromptsReceived[2]);
        }

        [Fact]
        public async Task GenerateAsync_TruncatedResponse_RecoversWithTryRepairJson()
        {
            // Arrange
            var mockKernelService = new Mock<IAiKernelService>();
            var validYaml = System.IO.File.ReadAllText(@"..\..\..\..\TaskPilot.AI\Prompts\Planning\WbsGeneration.yaml");
            var mockPromptLoader = new Mock<IPromptLoaderService>();
            mockPromptLoader.Setup(p => p.LoadAsync(It.IsAny<string>())).ReturnsAsync(validYaml);

            var mockChatService = new MockChatCompletionService();
            // First attempt has a truncated JSON that can be repaired (truncated during story 2)
            string repairableJson = "{\"userStories\": [{\"titleEn\": \"Story 1\", \"tasks\": []}, {\"titleEn\": \"Story 2\"";
            mockChatService.Responses.Enqueue(repairableJson);

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IChatCompletionService>(mockChatService);
            var kernel = builder.Build();

            mockKernelService.Setup(k => k.CreateKernel(It.IsAny<string>())).Returns(kernel);

            var agent = new WBSGenerationAgent(mockKernelService.Object, mockPromptLoader.Object);
            var snapshot = new RequirementsSnapshot { BusinessRequirements = new List<string> { "Req 1" } };

            // Act
            var result = await agent.GenerateAsync(snapshot, new List<string>(), new List<string>(), "General", new List<string>());

            // Assert
            Assert.NotNull(result);
            Assert.Single(mockChatService.PromptsReceived); // Repaired on first attempt
        }

        [Fact]
        public async Task GenerateAsync_ContinualFailure_ThrowsWbsGenerationException()
        {
            // Arrange
            var mockKernelService = new Mock<IAiKernelService>();
            var validYaml = System.IO.File.ReadAllText(@"..\..\..\..\TaskPilot.AI\Prompts\Planning\WbsGeneration.yaml");
            var mockPromptLoader = new Mock<IPromptLoaderService>();
            mockPromptLoader.Setup(p => p.LoadAsync(It.IsAny<string>())).ReturnsAsync(validYaml);

            var mockChatService = new MockChatCompletionService();
            // 3 unrepairable failures
            mockChatService.Responses.Enqueue("invalid 1");
            mockChatService.Responses.Enqueue("invalid 2");
            mockChatService.Responses.Enqueue("invalid 3");

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IChatCompletionService>(mockChatService);
            var kernel = builder.Build();

            mockKernelService.Setup(k => k.CreateKernel(It.IsAny<string>())).Returns(kernel);

            var agent = new WBSGenerationAgent(mockKernelService.Object, mockPromptLoader.Object);
            var snapshot = new RequirementsSnapshot { BusinessRequirements = new List<string> { "Req 1" } };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<WbsGenerationException>(() => 
                agent.GenerateAsync(snapshot, new List<string>(), new List<string>(), "General", new List<string>()));
            
            Assert.Contains("truncated or invalid JSON", ex.Message);
        }
    }
}
