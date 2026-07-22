using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Agents.RAG;
using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Options;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Persistence.InMemory;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Services;
using TaskPilot.AI.Services.Extraction;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.AI.Services.Requirements;

namespace TaskPilot.AI.Extensions
{
    public static class
        ServiceCollectionExtensions
    {
        public static IServiceCollection
            AddAiLayer(
                this IServiceCollection services,
                IConfiguration configuration)
        {
            services.Configure<QdrantOptions>(configuration.GetSection("Qdrant"));

            services.AddScoped<
                IAiKernelService,
                KernelService>();

            services.AddScoped<
                ICvAiService,
                CvAiService>();
            services.AddScoped<
                IPromptLoaderService,
                PromptLoaderService>();

            services.AddScoped<
                IEmbeddingService,
                EmbeddingService>();

            services.AddScoped<
                IVectorStore,
                QdrantVectorStore>();

            services.AddHostedService<QdrantInitializationHostedService>();

            // Document text extractors
            services.AddScoped<IDocumentTextExtractor, TextFileExtractor>();
            services.AddScoped<IDocumentTextExtractor, PdfTextExtractor>();
            services.AddScoped<IDocumentTextExtractor, DocxTextExtractor>();

            // Document visual extractors
            services.AddScoped<IDocumentVisualExtractor, MockPdfVisualExtractor>();

            services.AddScoped<
                 RequirementsOrchestrator>();

            services.AddScoped<
                 DocumentIngestionOrchestrator>();

            services.AddScoped<
                 RequirementDiscoveryOrchestrator>();

            services.AddScoped<
                 KnowledgeOrchestrator>();

            services.AddScoped<
                 IProjectAiChatOrchestrator,
                 ProjectAiChatOrchestrator>();

            services.AddSingleton<
                IRequirementSessionStore,
                InMemoryRequirementSessionStore>();

            services.AddSingleton<
                IDocumentStore,
                InMemoryDocumentStore>();

            // Ingestion agents
            services.AddScoped<
                DocumentCategorizationAgent>();
            services.AddScoped<
                AudioTranscriptionAgent>();
            services.AddScoped<
                ChunkingAgent>();
            services.AddScoped<
                DocumentQuestionResolutionAgent>();

            // Requirements agents
            services.AddScoped<
                InputProcessingAgent>();

            services.AddScoped<
                RequirementExtractionAgent>();

            services.AddScoped<
                AmbiguityDetectionAgent>();

            services.AddScoped<
                ClarificationAgent>();

            services.AddScoped<
                CompletenessEvaluatorAgent>();

            services.AddScoped<
                RequirementValidationAgent>();

            services.AddScoped<
                KnowledgeEvolutionAgent>();

            services.AddScoped<
                TaskPilot.AI.Services.Requirements.RequirementConsolidationEngine>();

            services.AddScoped<
                RequirementsBuilderAgent>();
            services.AddScoped<
                QuestionResolutionAgent>();
            services.AddScoped<
                RequirementAnalysisAgent>();
            services.AddScoped<
                VisualAnalysisAgent>();

            services.AddScoped<
                IRequirementReadinessEvaluator,
                RequirementReadinessEvaluator>();

            // RAG agents
            services.AddScoped<
                KnowledgeRetrievalAgent>();
            services.AddScoped<
                KnowledgeAnswerAgent>();

            // Planning agents
            services.AddScoped<
                WBSGenerationAgent>();
            services.AddScoped<
                TechStackAdvisorAgent>();
            services.AddScoped<
                SprintSuggestionAgent>();
            services.AddScoped<
                SprintRetrospectiveAgent>();
            services.AddScoped<
                RequiredSkillsEnrichmentAgent>();
            services.AddScoped<
                TaskPilot.AI.Agents.Assignment.IAssignmentExplanationAgent,
                TaskPilot.AI.Agents.Assignment.AssignmentExplanationAgent>();

            // Sprint Risk agents
            services.AddScoped<
                TaskPilot.AI.Agents.Sprint.SprintRiskDetectionAgent>();
            services.AddScoped<
                TaskPilot.AI.Agents.Sprint.WhatIfSimulationAgent>();
            services.AddScoped<
                TaskPilot.AI.Agents.Sprint.SprintBurnoutAgent>();

            return services;
        }
    }
}