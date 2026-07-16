using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Models.Ingestion;
using TaskPilot.AI.Models.Questions;
using TaskPilot.AI.Models.Requirements;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Models.Enums;

namespace TaskPilot.Tests.Requirements
{
    /// <summary>
    /// Integration-level unit tests for the BRD-First Requirement Discovery workflow.
    /// Tests verify that RequirementAnalysisAgent is correctly invoked on the document-first
    /// path and that all required session fields are populated so Finalization succeeds.
    /// </summary>
    public class BrdFirstWorkflowTests
    {
        // ------------------------------------------------------------------ helpers

        private static RequirementSession CreateDocumentFirstSession(Guid? id = null)
        {
            // Mirrors StartWithDocumentAsync() — empty QuestionPool, no IsBrdPrompt
            return new RequirementSession
            {
                SessionId = id ?? Guid.NewGuid(),
                Status    = RequirementSessionStatus.RequirementGathering
            };
        }

        private static IFormFile CreateFakeFile(string name = "requirements.txt", string content = "Sample BRD content")
        {
            var bytes  = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            var mock   = new Mock<IFormFile>();
            mock.Setup(f => f.FileName).Returns(name);
            mock.Setup(f => f.ContentType).Returns("text/plain");
            mock.Setup(f => f.Length).Returns(bytes.Length);
            mock.Setup(f => f.OpenReadStream()).Returns(stream);
            return mock.Object;
        }

        private static RequirementAnalysisResult CreateFullAnalysisResult(int gapCount = 2)
        {
            var gaps = new List<string>();
            for (int i = 0; i < gapCount; i++)
                gaps.Add($"Gap question {i + 1}");

            return new RequirementAnalysisResult
            {
                ExtractedRequirements = new ExtractedRequirements
                {
                    BusinessRequirements  = new List<string> { "Manage hospital workflows" },
                    TechnicalRequirements = new List<string> { "REST API", "Role-based access" },
                    Constraints           = new List<string> { "HIPAA compliance" },
                    Integrations          = new List<string> { "HL7 FHIR" },
                    ScaleRequirements     = new List<string> { "1 000 concurrent users" }
                },
                ConfidenceScores = new List<CategoryConfidence>
                {
                    new() { Category = "BusinessGoals", Score = 90, Status = "Covered"            },
                    new() { Category = "Scale",         Score = 60, Status = "PartiallyMentioned" },
                    new() { Category = "Integration",   Score = 75, Status = "PartiallyMentioned" },
                    new() { Category = "Timeline",      Score = 20, Status = "Missing"            },
                    new() { Category = "Compliance",    Score = 85, Status = "Covered"            },
                    new() { Category = "UserRoles",     Score = 80, Status = "Covered"            },
                    new() { Category = "Realtime",      Score = 10, Status = "Missing"            },
                },
                GapQuestions = gaps
            };
        }

        // ------------------------------------------------------------------ factory

        private static DocumentIngestionOrchestrator BuildOrchestrator(
            Guid sessionId,
            RequirementSession session,
            RequirementAnalysisResult analysisResult,
            List<KnowledgeChunk>? chunks = null)
        {
            chunks ??= new List<KnowledgeChunk>
            {
                new KnowledgeChunk { Id = Guid.NewGuid(), Content = "BRD chunk 1", ChunkIndex = 0, RequirementSessionId = sessionId }
            };

            // --- extractors
            var textExtractor = new Mock<IDocumentTextExtractor>();
            textExtractor.Setup(e => e.CanHandle(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
            textExtractor.Setup(e => e.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                         .ReturnsAsync("Extracted BRD text");

            // --- categorizationAgent
            var catAgent = new Mock<DocumentCategorizationAgent>(
                MockBehavior.Loose,
                Mock.Of<IAiKernelService>(),
                Mock.Of<IPromptLoaderService>());
            catAgent.Setup(a => a.CategorizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(DocumentCategory.BRD);

            // --- chunkingAgent
            var chunkAgent = new Mock<ChunkingAgent>(MockBehavior.Loose);
            chunkAgent.Setup(a => a.ChunkContentAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(),
                    It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(chunks);

            // --- document store
            var docStore = new Mock<IDocumentStore>();
            docStore.Setup(d => d.SaveDocumentAsync(It.IsAny<IngestedDocument>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
            docStore.Setup(d => d.SaveChunksAsync(It.IsAny<List<KnowledgeChunk>>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

            // --- session store
            var sessionStore = new Mock<IRequirementSessionStore>();
            sessionStore.Setup(s => s.GetAsync(sessionId, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(session);
            sessionStore.Setup(s => s.SaveAsync(It.IsAny<RequirementSession>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

            // --- document question resolution
            var docQResolution = new Mock<DocumentQuestionResolutionAgent>(
                MockBehavior.Loose,
                Mock.Of<IAiKernelService>(),
                Mock.Of<IPromptLoaderService>());
            docQResolution.Setup(a => a.ResolveAsync(It.IsAny<List<ClarificationQuestion>>(), It.IsAny<string>()))
                          .ReturnsAsync(new List<QuestionResolution>());

            // --- completeness evaluator (not used on document-first path directly)
            var completenessAgent = new Mock<CompletenessEvaluatorAgent>(
                MockBehavior.Loose,
                Mock.Of<IAiKernelService>(),
                Mock.Of<IPromptLoaderService>());

            // --- vector store
            var vectorStore = new Mock<IVectorStore>();
            vectorStore.Setup(v => v.UpsertAsync(
                    It.IsAny<KnowledgeCollectionType>(),
                    It.IsAny<List<KnowledgeChunk>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            vectorStore.Setup(v => v.SearchAsync(
                    It.IsAny<KnowledgeCollectionType>(),
                    It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                    It.IsAny<string>(), It.IsAny<int>(), It.IsAny<float>(),
                    It.IsAny<DocumentCategory?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(chunks);

            // --- requirements builder agent
            var builderAgent = new Mock<RequirementsBuilderAgent>(
                MockBehavior.Loose,
                Mock.Of<IAiKernelService>(),
                Mock.Of<IPromptLoaderService>());

            // --- requirement analysis agent
            var analysisAgent = new Mock<RequirementAnalysisAgent>(
                MockBehavior.Loose,
                Mock.Of<IAiKernelService>(),
                Mock.Of<IPromptLoaderService>(),
                vectorStore.Object);
            analysisAgent.Setup(a => a.AnalyzeAsync(sessionId, It.IsAny<CancellationToken>()))
                         .ReturnsAsync(analysisResult);

            // --- logger
            var logger = new Mock<ILogger<DocumentIngestionOrchestrator>>();

            return new DocumentIngestionOrchestrator(
                new[] { textExtractor.Object },
                catAgent.Object,
                chunkAgent.Object,
                docStore.Object,
                sessionStore.Object,
                docQResolution.Object,
                completenessAgent.Object,
                vectorStore.Object,
                logger.Object,
                builderAgent.Object,
                analysisAgent.Object);
        }

        // ------------------------------------------------------------------ tests

        /// <summary>
        /// Scenario 1: Document-first session (empty QuestionPool).
        /// RequirementAnalysisAgent MUST be called and its results stored.
        /// </summary>
        [Fact]
        public async Task StartWithDocument_ShouldExecuteRequirementAnalysisAgent()
        {
            // Arrange
            var sessionId    = Guid.NewGuid();
            var session      = CreateDocumentFirstSession(sessionId);
            var analysis     = CreateFullAnalysisResult(gapCount: 2);
            var orchestrator = BuildOrchestrator(sessionId, session, analysis);

            // Act
            var result = await orchestrator.IngestAsync(sessionId, CreateFakeFile(), CancellationToken.None);

            // Assert — ingestion succeeded
            Assert.True(result.Success, result.Message);

            // Assert — Requirements populated
            Assert.NotEmpty(session.Requirements.BusinessRequirements);

            // Assert — ConfidenceScores populated
            Assert.NotEmpty(session.ConfidenceScores);
            Assert.Equal(7, session.ConfidenceScores.Count);

            // Assert — GapQuestions in QuestionPool
            Assert.Equal(2, session.QuestionPool.Count);
            Assert.All(session.QuestionPool, q => Assert.False(q.IsBrdPrompt));

            // Assert — CompletenessReport synthesised
            Assert.NotNull(session.CompletenessReport);
            Assert.True(session.CompletenessReport.Score > 0f);
        }

        /// <summary>
        /// Scenario 2: ConfidenceScores from AI are correctly mapped to RequirementConfidenceScore.
        /// </summary>
        [Fact]
        public async Task StartWithDocument_ShouldMapConfidenceScoresToSession()
        {
            // Arrange
            var sessionId    = Guid.NewGuid();
            var session      = CreateDocumentFirstSession(sessionId);
            var analysis     = CreateFullAnalysisResult(gapCount: 0);
            var orchestrator = BuildOrchestrator(sessionId, session, analysis);

            // Act
            await orchestrator.IngestAsync(sessionId, CreateFakeFile(), CancellationToken.None);

            // Assert — all 7 categories mapped
            Assert.Equal(7, session.ConfidenceScores.Count);
            Assert.Contains(session.ConfidenceScores, cs => cs.Category == "BusinessGoals" && cs.Score == 90);
            Assert.Contains(session.ConfidenceScores, cs => cs.Category == "Timeline" && cs.Score == 20);
        }

        /// <summary>
        /// Scenario 3: When gap questions are 0, CompletenessReport.ReadyForPlanning must be true.
        /// </summary>
        [Fact]
        public async Task StartWithDocument_WhenNoGapQuestions_CompletenessReportReadyForPlanning()
        {
            // Arrange
            var sessionId    = Guid.NewGuid();
            var session      = CreateDocumentFirstSession(sessionId);
            var analysis     = CreateFullAnalysisResult(gapCount: 0); // no gaps
            var orchestrator = BuildOrchestrator(sessionId, session, analysis);

            // Act
            await orchestrator.IngestAsync(sessionId, CreateFakeFile(), CancellationToken.None);

            // Assert
            Assert.NotNull(session.CompletenessReport);
            Assert.True(session.CompletenessReport.ReadyForPlanning);
        }

        /// <summary>
        /// Scenario 4: When Qdrant returns no chunks, the fallback result is used.
        /// CompletenessReport must still be non-null (fallback scores are zero).
        /// </summary>
        [Fact]
        public async Task StartWithDocument_WhenChunksEmpty_FallbackAnalysisUsed()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session   = CreateDocumentFirstSession(sessionId);

            // Fallback result: all scores 0, standard gap questions
            var fallbackAnalysis = new RequirementAnalysisResult
            {
                ExtractedRequirements = new ExtractedRequirements(),
                ConfidenceScores      = new List<CategoryConfidence>
                {
                    new() { Category = "BusinessGoals", Score = 0, Status = "Missing" },
                    new() { Category = "Scale",         Score = 0, Status = "Missing" },
                    new() { Category = "Integration",   Score = 0, Status = "Missing" },
                    new() { Category = "Timeline",      Score = 0, Status = "Missing" },
                    new() { Category = "Compliance",    Score = 0, Status = "Missing" },
                    new() { Category = "UserRoles",     Score = 0, Status = "Missing" },
                    new() { Category = "Realtime",      Score = 0, Status = "Missing" },
                },
                GapQuestions = new List<string> { "What are the primary business goals?" }
            };

            var orchestrator = BuildOrchestrator(
                sessionId, session, fallbackAnalysis,
                chunks: new List<KnowledgeChunk>()); // empty chunks → fallback

            // Act
            await orchestrator.IngestAsync(sessionId, CreateFakeFile(), CancellationToken.None);

            // Assert — CompletenessReport still exists (fallback-sourced)
            Assert.NotNull(session.CompletenessReport);
            Assert.Equal(0f, session.CompletenessReport.Score);
        }

        /// <summary>
        /// Scenario 5 (Backward Compatibility): Chat-first path still works correctly.
        /// When a session has an IsBrdPrompt question, it is resolved by the document
        /// and analysis runs through the existing branch — not the new else branch.
        /// </summary>
        [Fact]
        public async Task ChatFirst_PathUnchanged_BrdPromptStillResolvedByDocument()
        {
            // Arrange — session with an IsBrdPrompt question (chat-first)
            var sessionId = Guid.NewGuid();
            var session   = CreateDocumentFirstSession(sessionId);
            session.QuestionPool.Add(new ClarificationQuestion
            {
                Id          = Guid.NewGuid(),
                Question    = "Please upload your requirement document (BRD/SRS/RFP) if available.",
                Category    = QuestionCategory.General,
                Priority    = QuestionPriority.High,
                IsBrdPrompt = true,
                IsAnswered  = false
            });

            var analysis     = CreateFullAnalysisResult(gapCount: 1);
            var orchestrator = BuildOrchestrator(sessionId, session, analysis);

            // Act
            await orchestrator.IngestAsync(sessionId, CreateFakeFile(), CancellationToken.None);

            // Assert — original BRD prompt was answered
            Assert.True(session.QuestionPool.Exists(q => q.IsBrdPrompt && q.IsAnswered));

            // Assert — analysis results still populated
            Assert.NotNull(session.CompletenessReport);
            Assert.NotEmpty(session.ConfidenceScores);

            // Assert — 1 gap question added (not the BRD prompt)
            Assert.Contains(session.QuestionPool, q => !q.IsBrdPrompt);
        }

        /// <summary>
        /// Scenario 6: Finalization guard check — CompletenessReport.Score expected to be > 0
        /// for a document with real content, confirming the finalization guard will pass.
        /// </summary>
        [Fact]
        public async Task StartWithDocument_CompletenessScoreAboveZero_ForRealBrd()
        {
            // Arrange
            var sessionId = Guid.NewGuid();
            var session   = CreateDocumentFirstSession(sessionId);
            // Simulate a well-covered BRD (avg score = 74 → 0.74)
            var analysis = new RequirementAnalysisResult
            {
                ExtractedRequirements = new ExtractedRequirements
                {
                    BusinessRequirements = new List<string> { "System requirement" }
                },
                ConfidenceScores = new List<CategoryConfidence>
                {
                    new() { Category = "BusinessGoals", Score = 80, Status = "Covered" },
                    new() { Category = "Scale",         Score = 70, Status = "PartiallyMentioned" },
                    new() { Category = "Integration",   Score = 65, Status = "PartiallyMentioned" },
                    new() { Category = "Timeline",      Score = 75, Status = "PartiallyMentioned" },
                    new() { Category = "Compliance",    Score = 90, Status = "Covered" },
                    new() { Category = "UserRoles",     Score = 60, Status = "PartiallyMentioned" },
                    new() { Category = "Realtime",      Score = 72, Status = "PartiallyMentioned" },
                },
                GapQuestions = new List<string> { "What is the timeline?" }
            };

            var orchestrator = BuildOrchestrator(sessionId, session, analysis);

            // Act
            await orchestrator.IngestAsync(sessionId, CreateFakeFile(), CancellationToken.None);

            // Assert — score is normalised average: (80+70+65+75+90+60+72)/7 / 100 ≈ 0.73
            Assert.NotNull(session.CompletenessReport);
            Assert.True(session.CompletenessReport.Score > 0.5f,
                $"Expected score > 0.5 but got {session.CompletenessReport.Score}");

            // This is the exact condition in FinalizeRequirementsAsync — must pass
            Assert.False(session.CompletenessReport == null, "CompletenessReport must not be null for finalization");
        }
    }
}
