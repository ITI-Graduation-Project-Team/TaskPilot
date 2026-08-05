using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Moq;
using Xunit;
using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Enums;
using TaskPilot.AI.Enums;

namespace TaskPilot.Tests.AI
{
    public class RagFallbackTests
    {
        [Fact]
        public async Task BuildAsync_WithLargeContext_TriggersFallbackAndRetrievesDiagramChunks()
        {
            // Arrange
            var kernelServiceMock = new Mock<IAiKernelService>();
            var promptLoaderMock = new Mock<IPromptLoaderService>();
            var vectorStoreMock = new Mock<IVectorStore>();

            var kernel = new Kernel();
            kernelServiceMock.Setup(x => x.CreateKernel(It.IsAny<string>())).Returns(kernel);

            var validYaml = @"name: Mock
description: mock
template: mock";
            promptLoaderMock.Setup(x => x.LoadAsync(It.IsAny<string>()))
                .ReturnsAsync(validYaml);

            // Create a large text string to exceed token limits
            var largeText = new string('A', 500000); // 500k characters > 100k tokens

            var session = new RequirementSession
            {
                SessionId = Guid.NewGuid(),
                Knowledge = new SessionKnowledgeContext
                {
                    Documents = new List<IngestedDocument>
                    {
                        new IngestedDocument
                        {
                            Id = Guid.NewGuid(),
                            FileName = "LargeBRD.pdf",
                            ExtractedText = largeText,
                            Category = DocumentCategory.Diagram
                        }
                    }
                }
            };

            // Mock vector store to return a diagram chunk
            var diagramChunk = new KnowledgeChunk
            {
                Id = Guid.NewGuid(),
                Category = DocumentCategory.Diagram,
                Content = "Diagram Type: UML Class Diagram\nDescription: Main ERD\nStructured Metadata: Entities: Invoice, InvoiceLine"
            };

            string capturedQuery = null;
            vectorStoreMock.Setup(x => x.SearchAsync(
                It.IsAny<KnowledgeCollectionType>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<DocumentCategory?>(),
                It.IsAny<CancellationToken>()))
                .Callback<KnowledgeCollectionType, Guid?, Guid?, Guid?, string, int, float, DocumentCategory?, CancellationToken>(
                    (col, sId, pId, cId, query, k, min, filter, ct) => capturedQuery = query)
                .ReturnsAsync(new List<KnowledgeChunk> { diagramChunk });

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<RequirementsBuilderAgent>>();
            var telemetryMock = new Mock<ITelemetryAccumulator>();
            var agent = new RequirementsBuilderAgent(kernelServiceMock.Object, promptLoaderMock.Object, vectorStoreMock.Object, loggerMock.Object, telemetryMock.Object);

            // Act
            try
            {
                // We expect an exception because the Kernel has no mock function behavior set up and will fail to parse JSON.
                // But before it fails, it will have called vectorStore.SearchAsync.
                await agent.BuildAsync(session);
            }
            catch
            {
                // Ignore the exception from kernel/JSON parsing.
            }

            // Assert
            Assert.NotNull(capturedQuery);
            Assert.Contains("entities", capturedQuery.ToLower());
            Assert.Contains("relationships", capturedQuery.ToLower());
            Assert.Contains("diagram", capturedQuery.ToLower());
        }
    }
}
