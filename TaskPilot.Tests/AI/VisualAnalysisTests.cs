using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;
using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace TaskPilot.Tests.AI
{
    public class VisualAnalysisTests
    {
        [Fact]
        public async Task AnalyzeImageAsync_ReturnsStructuredEntities()
        {
            // Arrange
            var kernelServiceMock = new Mock<IAiKernelService>();
            var promptLoaderMock = new Mock<IPromptLoaderService>();

            var kernel = new Kernel();
            
            var chatCompletionMock = new Mock<IChatCompletionService>();
            var mockJson = @"{
              ""DiagramType"": ""UML Class Diagram"",
              ""SummaryDescription"": ""Test diagram"",
              ""ExtractedText"": ""Test extracted"",
              ""Entities"": [
                {
                  ""Name"": ""Invoice"",
                  ""Attributes"": [""InvoiceId""],
                  ""Relationships"": [""1-to-Many with InvoiceLine""]
                }
              ],
              ""ExtractedRequirements"": []
            }";
            
            chatCompletionMock.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ChatMessageContent> { new ChatMessageContent(AuthorRole.Assistant, mockJson) });

            // Add the mock service to the kernel's service provider if possible, but Semantic Kernel doesn't allow easy adding to an existing Kernel instance without cloning. 
            // We can just construct a kernel with the service.
            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IChatCompletionService>(chatCompletionMock.Object);
            var configuredKernel = builder.Build();

            kernelServiceMock.Setup(x => x.CreateKernel(It.IsAny<string>())).Returns(configuredKernel);

            var validYaml = @"name: Mock
description: mock
template: mock";
            promptLoaderMock.Setup(x => x.LoadAsync(It.IsAny<string>()))
                .ReturnsAsync(validYaml);

            var agent = new VisualAnalysisAgent(kernelServiceMock.Object, promptLoaderMock.Object);

            // Act
            var result = await agent.AnalyzeImageAsync("url", new byte[] { 0x01 }, "image/png");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("UML Class Diagram", result.DiagramType);
            Assert.Single(result.Entities);
            Assert.Equal("Invoice", result.Entities[0].Name);
            Assert.Contains("1-to-Many with InvoiceLine", result.Entities[0].Relationships);
        }
    }
}
