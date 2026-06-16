using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Services;
using TaskPilot.AI.Agents.ContextAdvisor;
using TaskPilot.AI.Agents.Requirements;
using TaskPilot.AI.Agents.Ingestion;
using TaskPilot.AI.Orchestrators;
using TaskPilot.AI.Persistence.InMemory;
using TaskPilot.AI.Persistence.Interfaces;
using TaskPilot.AI.RAG;
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
                this IServiceCollection services)
        {
            services.AddSingleton<
                IAiKernelService,
                KernelService>();

            services.AddScoped<
                ICvAiService,
                CvAiService>();
            services.AddScoped<
                IPromptLoaderService,
                PromptLoaderService>();

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
                 ContextAdvisorOrchestrator>();

            services.AddSingleton<
                IRequirementSessionStore,
                InMemoryRequirementSessionStore>();

            services.AddSingleton<
                IDocumentStore,
                InMemoryDocumentStore>();

            services.AddSingleton<
                IContextAdvisorConversationStore,
                InMemoryContextAdvisorConversationStore>();

            services.AddScoped<
                IProjectKnowledgeSearchService,
                ProjectKnowledgeSearchService>();

            // Ingestion agents
            services.AddScoped<
                DocumentCategorizationAgent>();
            services.AddScoped<
                AudioTranscriptionAgent>();
            services.AddScoped<
                ChunkingAgent>();

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

            // Context advisor agents
            services.AddScoped<
                AgileCoachAgent>();

            return services;
        }
    }
}
