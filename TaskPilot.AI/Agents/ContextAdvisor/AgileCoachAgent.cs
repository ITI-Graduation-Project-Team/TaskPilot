using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Models.ContextAdvisor;
using TaskPilot.AI.Models.Session;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.ContextAdvisor
{
    public class AgileCoachAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public AgileCoachAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<ContextAdvisorAnswerResponse> AnswerAsync(
            TaskContextRequest taskContext,
            string question,
            IReadOnlyCollection<RetrievedKnowledgeChunk> retrievedChunks,
            IReadOnlyCollection<ConversationMessage> conversationMemory,
            CancellationToken cancellationToken = default)
        {
            var kernel =
                _kernelService
                    .CreateGeminiKernel(ModelConstants.GeminiFast);

            var prompt =
                await _promptLoader
                    .LoadAsync("ContextAdvisor/Answer.yaml");

            var function =
                KernelFunctionYaml
                    .FromPromptYaml(prompt);

            var arguments =
                KernelArgumentsFactory
                    .CreateDeterministicArguments();

            arguments["taskContext"] =
                BuildTaskContext(taskContext);

            arguments["conversationMemory"] =
                BuildConversationMemory(conversationMemory);

            arguments["retrievedKnowledge"] =
                BuildRetrievedKnowledge(retrievedChunks);

            arguments["question"] =
                question;

            var result =
                await kernel.InvokeAsync(function, arguments, cancellationToken);

            var json =
                JsonCleaner.Clean(result.ToString());

            var modelResult =
                JsonSerializer.Deserialize<AnswerModelResult>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (modelResult is null)
            {
                throw new Exception($"Invalid context advisor answer JSON: {json}");
            }

            var citations =
                BuildCitations(retrievedChunks);

            return new ContextAdvisorAnswerResponse
            {
                Answer = EnsureCitationMarker(modelResult.Answer, citations),
                Citations = citations,
                SuggestedFollowUps = modelResult.SuggestedFollowUps
                    .Where(followUp => !string.IsNullOrWhiteSpace(followUp))
                    .ToList()
            };
        }

        public async Task<ContextSummaryResponse> SummarizeAsync(
            TaskContextRequest taskContext,
            IReadOnlyCollection<RetrievedKnowledgeChunk> retrievedChunks,
            CancellationToken cancellationToken = default)
        {
            var kernel =
                _kernelService
                    .CreateGeminiKernel(ModelConstants.GeminiFast);

            var prompt =
                await _promptLoader
                    .LoadAsync("ContextAdvisor/Summary.yaml");

            var function =
                KernelFunctionYaml
                    .FromPromptYaml(prompt);

            var arguments =
                KernelArgumentsFactory
                    .CreateDeterministicArguments();

            arguments["taskContext"] =
                BuildTaskContext(taskContext);

            arguments["retrievedKnowledge"] =
                BuildRetrievedKnowledge(retrievedChunks);

            var result =
                await kernel.InvokeAsync(function, arguments, cancellationToken);

            var json =
                JsonCleaner.Clean(result.ToString());

            var modelResult =
                JsonSerializer.Deserialize<SummaryModelResult>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (modelResult is null)
            {
                throw new Exception($"Invalid context summary JSON: {json}");
            }

            return new ContextSummaryResponse
            {
                Summary = EnsureCitationMarker(modelResult.Summary, BuildCitations(retrievedChunks)),
                CodebaseNotes = modelResult.CodebaseNotes,
                RelatedPastTasks = modelResult.RelatedPastTasks,
                TechStackContext = modelResult.TechStackContext,
                SuggestedImplementationGuidance = modelResult.SuggestedImplementationGuidance,
                Citations = BuildCitations(retrievedChunks)
            };
        }

        private static string BuildTaskContext(TaskContextRequest taskContext)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"ProjectId: {taskContext.ProjectId?.ToString() ?? "not provided"}");
            builder.AppendLine($"TaskId: {taskContext.TaskId?.ToString() ?? "not provided"}");
            builder.AppendLine($"Title: {taskContext.TaskTitle}");
            builder.AppendLine($"Description: {taskContext.TaskDescription}");
            builder.AppendLine($"Acceptance Criteria: {taskContext.AcceptanceCriteria}");
            builder.AppendLine($"Technical Summary: {taskContext.TechnicalSummary}");
            builder.AppendLine("Related Past Tasks:");

            foreach (var pastTask in taskContext.RelatedPastTasks)
            {
                builder.AppendLine($"- {pastTask}");
            }

            return builder.ToString();
        }

        private static string BuildConversationMemory(
            IReadOnlyCollection<ConversationMessage> messages)
        {
            return string.Join(
                "\n",
                messages
                    .OrderBy(message => message.Timestamp)
                    .TakeLast(12)
                    .Select(message => $"{message.Role}: {message.Message}"));
        }

        private static string BuildRetrievedKnowledge(
            IReadOnlyCollection<RetrievedKnowledgeChunk> chunks)
        {
            if (!chunks.Any())
            {
                return "No project knowledge chunks were retrieved.";
            }

            var builder = new StringBuilder();
            var number = 1;

            foreach (var chunk in chunks)
            {
                builder.AppendLine($"[{number}] {chunk.FileName} - chunk {chunk.ChunkIndex}");
                builder.AppendLine(chunk.Content);
                builder.AppendLine();
                number++;
            }

            return builder.ToString();
        }

        private static List<ContextCitation> BuildCitations(
            IReadOnlyCollection<RetrievedKnowledgeChunk> chunks)
        {
            return chunks
                .Select((chunk, index) => new ContextCitation
                {
                    Number = index + 1,
                    DocumentId = chunk.DocumentId,
                    ChunkId = chunk.ChunkId,
                    FileName = chunk.FileName,
                    ChunkIndex = chunk.ChunkIndex,
                    SourceUrl = chunk.SourceUrl,
                    Snippet = CreateSnippet(chunk.Content)
                })
                .ToList();
        }

        private static string CreateSnippet(string content)
        {
            var normalized =
                string.Join(
                    " ",
                    content.Split(
                        Array.Empty<char>(),
                        StringSplitOptions.RemoveEmptyEntries));

            return normalized.Length <= 220
                ? normalized
                : $"{normalized[..220]}...";
        }

        private static string EnsureCitationMarker(
            string answer,
            IReadOnlyCollection<ContextCitation> citations)
        {
            if (!citations.Any()
                || answer.Contains("[", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(answer))
            {
                return answer;
            }

            return $"{answer.Trim()} [1]";
        }

        private sealed class AnswerModelResult
        {
            public string Answer { get; set; } = string.Empty;

            public List<string> SuggestedFollowUps { get; set; } = new();
        }

        private sealed class SummaryModelResult
        {
            public string Summary { get; set; } = string.Empty;

            public List<string> CodebaseNotes { get; set; } = new();

            public List<string> RelatedPastTasks { get; set; } = new();

            public List<string> TechStackContext { get; set; } = new();

            public List<string> SuggestedImplementationGuidance { get; set; } = new();
        }
    }
}
