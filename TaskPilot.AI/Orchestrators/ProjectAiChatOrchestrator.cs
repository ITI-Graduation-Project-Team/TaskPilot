using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Plugins;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Orchestrators
{
    public class ProjectAiChatOrchestrator : IProjectAiChatOrchestrator
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;
        private readonly IAiProjectChatService _chatService;
        private readonly IAiBacklogService _backlogService;
        private readonly ILogger<ProjectAiChatOrchestrator> _logger;

        public ProjectAiChatOrchestrator(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader,
            IAiProjectChatService chatService,
            IAiBacklogService backlogService,
            ILogger<ProjectAiChatOrchestrator> logger)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
            _chatService = chatService;
            _backlogService = backlogService;
            _logger = logger;
        }

        private static string DetectLanguage(string text)
        {
            // Arabic Unicode blocks: Arabic, Arabic Supplement, Arabic Extended-A
            return System.Text.RegularExpressions.Regex.IsMatch(
                text, @"[\u0600-\u06FF\u0750-\u077F\u08A0-\u08FF]")
                ? "ar"
                : "en";
        }

        public async Task<string> ProcessBacklogChatAsync(Guid projectId, string message, CancellationToken cancellationToken = default)
        {
            // 1. Get existing session
            var sessionResult = await _chatService.GetOrCreateSessionAsync(projectId, cancellationToken);
            if (!sessionResult.IsSuccess)
                throw new Exception($"Failed to retrieve or create chat session for project {projectId}");

            var session = sessionResult.Value;

            // 2. Initialize Semantic Kernel Chat History
            var systemPrompt = await _promptLoader.LoadAsync("Backlog/backlog_chat.yaml");
            
            if (!systemPrompt.Contains("{{$lang}}"))
            {
                systemPrompt += "\n[System] Language to use for your response: {{$lang}}";
            }

            var lang = DetectLanguage(message);
            var arguments = new KernelArguments();
            arguments["lang"] = lang;

            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var factory = new KernelPromptTemplateFactory();
            var renderedPrompt = await factory.Create(new PromptTemplateConfig(systemPrompt)).RenderAsync(kernel, arguments);

            var chatHistory = new ChatHistory(renderedPrompt);

            // Inject the backlog context BEFORE replaying chat history
            var backlogResult = await _backlogService.GetBacklogAsync(projectId);
            if (backlogResult.IsSuccess && backlogResult.Value?.UserStories != null)
            {
                var lines = backlogResult.Value.UserStories.Select(s =>
                    $"  - StoryId: {s.Id} | Title: {s.Title} | Priority: {s.Priority} | Status: {s.Status} | Tasks: {s.Tasks.Count}");
                var backlogBlock = "CURRENT BACKLOG (use these exact IDs for update/delete):\n" + string.Join("\n", lines);
                chatHistory.AddSystemMessage(backlogBlock);
            }
            else
            {
                _logger.LogWarning("Failed to retrieve backlog context for project {ProjectId}.", projectId);
            }

            // TODO: Implement context window trimming for long sessions 
            // (see comment below). Truncate oldest messages while always 
            // preserving the system prompt.
            // Add previous messages (skip if they are too many, but let's keep it simple for now)
            foreach (var msg in session.Messages)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    chatHistory.AddUserMessage(msg.Content);
                }
                else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    chatHistory.AddAssistantMessage(msg.Content);
                }
            }

            // 3. Add new user message
            chatHistory.AddUserMessage(message);

            // 5. Get chat completion service
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // 6. Execute model (NO tool calling for normal chat)
            var aiResponse = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                null,
                kernel,
                cancellationToken);

            var assistantReply = aiResponse.Content ?? string.Empty;

            // 7. Persist the new messages in DB
            var persistResult = await _chatService.AppendMessagesAsync(
                projectId,
                new System.Collections.Generic.List<(string, string)>
                {
                    ("User", message),
                    ("Assistant", assistantReply)
                },
                cancellationToken);

            if (!persistResult.IsSuccess)
                _logger.LogError(
                    "Failed to persist chat messages for project {ProjectId}: {Error}", 
                    projectId, persistResult.Error.Description);

            return assistantReply;
        }

        public async Task<string> ConfirmBacklogUpdatesAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            // 1. Get existing session to fetch the agreed-upon updates
            var sessionResult = await _chatService.GetOrCreateSessionAsync(projectId, cancellationToken);
            if (!sessionResult.IsSuccess)
                throw new Exception($"Failed to retrieve chat session for project {projectId}");

            var session = sessionResult.Value;

            // 2. Initialize Semantic Kernel
            var pluginLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BacklogEditorPlugin>.Instance;
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var plugin = new BacklogEditorPlugin(_backlogService, pluginLogger);
            kernel.Plugins.AddFromObject(plugin, "BacklogEditor");

            // 3. Build history
            var systemPrompt = await _promptLoader.LoadAsync("Backlog/backlog_editor.yaml");
            var chatHistory = new ChatHistory(systemPrompt);

            var backlogResult = await _backlogService.GetBacklogAsync(projectId);
            if (backlogResult.IsSuccess && backlogResult.Value?.UserStories != null)
            {
                var backlogBlock = "CURRENT BACKLOG (use these exact IDs for update/delete):\n";
                foreach (var s in backlogResult.Value.UserStories)
                {
                    backlogBlock += $"  - StoryId: {s.Id} | Title: {s.Title} | Priority: {s.Priority} | Status: {s.Status}\n";
                    foreach (var t in s.Tasks)
                    {
                        backlogBlock += $"      - TaskId: {t.Id} | Title: {t.Title}\n";
                    }
                }
                chatHistory.AddSystemMessage(backlogBlock);
            }
            else
            {
                _logger.LogWarning("Failed to retrieve backlog context for project {ProjectId}.", projectId);
            }

            foreach (var msg in session.Messages)
            {
                if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    chatHistory.AddUserMessage(msg.Content);
                }
                else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    chatHistory.AddAssistantMessage(msg.Content);
                }
            }

            // 4. Force execution of tool calls with idempotency rules
            chatHistory.AddUserMessage($@"The PM has clicked Confirm Backlog. This is the execution phase — not planning, not discussion.

CURRENT BACKLOG STATE is injected above as a system message. It represents what exists in the database RIGHT NOW.

Your job — execute immediately:
1. Review the full conversation history above.
2. Find every change you planned, promised, or agreed to make — including additions, updates, AND deletions.
3. Execute ALL of them now using the available tools.
4. For CREATE: Only create if the story does not already exist in the current backlog by title.
5. For UPDATE: Use the exact StoryId or TaskId from the backlog context.
6. For DELETE: Use the exact StoryId or TaskId from the backlog context. A story the PM asked to remove MUST be deleted now — do not skip it because it was previously described as 'planned'.
7. Do NOT ask for confirmation — the PM already confirmed by clicking the button.
8. After executing all tools, return a concise summary of exactly what was added, updated, or deleted. If nothing needed to change, say so explicitly.

The current ProjectId is {projectId}. Use this exact ID for all create operations.");

            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // This will execute the tools
            var response = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken);

            var reply = response.Content;
            return string.IsNullOrWhiteSpace(reply) ? "Backlog updated successfully." : reply;
        }
    }
}
