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

        public async Task<string> ProcessBacklogChatAsync(Guid projectId, string message, CancellationToken cancellationToken = default)
        {
            // 1. Get existing session
            var sessionResult = await _chatService.GetOrCreateSessionAsync(projectId, cancellationToken);
            if (!sessionResult.IsSuccess)
                throw new Exception($"Failed to retrieve or create chat session for project {projectId}");

            var session = sessionResult.Value;

            // 2. Initialize Semantic Kernel Chat History
            var systemPrompt = await _promptLoader.LoadAsync("Backlog/backlog_chat.yaml");
            var chatHistory = new ChatHistory(systemPrompt);

            // Inject the backlog context BEFORE replaying chat history
            var backlogResult = await _backlogService.GetBacklogAsync(projectId);
            if (backlogResult.IsSuccess && backlogResult.Value?.UserStories != null)
            {
                var lines = backlogResult.Value.UserStories.Select(s =>
                    $"  - StoryId: {s.Id} | Title: {s.TitleEn} | Priority: {s.Priority} | Status: {s.Status} | Tasks: {s.Tasks.Count}");
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

            // 4. Create Kernel
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);

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
            var kernel = _kernelService.CreateKernel(ModelConstants.PowerfulModel);
            var plugin = new BacklogEditorPlugin(_backlogService);
            kernel.Plugins.AddFromObject(plugin, "BacklogEditor");

            // 3. Build history
            var systemPrompt = await _promptLoader.LoadAsync("Backlog/backlog_editor.yaml");
            var chatHistory = new ChatHistory(systemPrompt);

            var backlogResult = await _backlogService.GetBacklogAsync(projectId);
            if (backlogResult.IsSuccess && backlogResult.Value?.UserStories != null)
            {
                var lines = backlogResult.Value.UserStories.Select(s =>
                    $"  - StoryId: {s.Id} | Title: {s.TitleEn} | Priority: {s.Priority} | Status: {s.Status} | Tasks: {s.Tasks.Count}");
                var backlogBlock = "CURRENT BACKLOG (use these exact IDs for update/delete):\n" + string.Join("\n", lines);
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
            chatHistory.AddUserMessage($@"The PM has clicked Confirm Backlog.

CURRENT BACKLOG STATE is already injected above as a system message. It represents what currently exists in the database RIGHT NOW.

Your job:
1. Review the conversation history above.
2. Identify ONLY the changes the PM agreed to that are NOT already reflected in the current backlog.
3. For CREATE: Only create a story if a story with the same or very similar title does NOT already exist in the current backlog. If it exists, skip it.
4. For UPDATE: Only update stories that exist in the current backlog using their exact StoryId.
5. For DELETE: Only delete stories that exist in the current backlog using their exact StoryId.
6. Do NOT re-execute changes that were already applied in a previous confirm session.
7. After executing all tools, return a concise summary of exactly what was added, updated, or deleted. If nothing needed to change, say so explicitly.

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
