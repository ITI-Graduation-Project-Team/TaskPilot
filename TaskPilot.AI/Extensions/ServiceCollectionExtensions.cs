using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Services;
using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Agents.RAG;
using TaskPilot.AI.Options;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Persistence.InMemory;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.Services;
using TaskPilot.AI.Services.Extraction;
using TaskPilot.AI.Services.Interfaces;

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

            services.AddSingleton<
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

            //Regarding the orchestrator, we can consider it as a higher-level service that coordinates multiple lower-level services.
            services.AddScoped<
                 RequirementsOrchestrator>();

            services.AddScoped<
                 DocumentIngestionOrchestrator>();

            services.AddScoped<
                 KnowledgeOrchestrator>();

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
                RequirementsBuilderAgent>();
            services.AddScoped<
                QuestionResolutionAgent>();

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

            return services;
        }
    }
}