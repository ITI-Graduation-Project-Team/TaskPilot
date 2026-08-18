using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TaskPilot.AI.Services;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Extensions;

public static class ChatCompletionTelemetryExtensions
{
    public static async Task<ChatMessageContent> GetChatMessageContentWithTelemetryAsync(
        this IChatCompletionService service,
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings,
        Kernel kernel,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationType = "")
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await service.GetChatMessageContentAsync(
                chatHistory, executionSettings, kernel, cancellationToken);
            await RecordAsync(kernel, result.Metadata, operationType, stopwatch.ElapsedMilliseconds, "Success", null);
            return result;
        }
        catch (Exception ex)
        {
            await RecordAsync(kernel, null, operationType, stopwatch.ElapsedMilliseconds, "Failed", ex.Message);
            throw;
        }
    }

    public static async Task<ChatMessageContent> GetChatMessageContentWithTelemetryAsync(
        this IChatCompletionService service,
        string prompt,
        PromptExecutionSettings? executionSettings,
        Kernel kernel,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string operationType = "")
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await service.GetChatMessageContentAsync(
                prompt, executionSettings, kernel, cancellationToken);
            await RecordAsync(kernel, result.Metadata, operationType, stopwatch.ElapsedMilliseconds, "Success", null);
            return result;
        }
        catch (Exception ex)
        {
            await RecordAsync(kernel, null, operationType, stopwatch.ElapsedMilliseconds, "Failed", ex.Message);
            throw;
        }
    }

    private static Task RecordAsync(
        Kernel kernel,
        IReadOnlyDictionary<string, object?>? metadata,
        string operationType,
        long elapsedMs,
        string status,
        string? errorMessage)
    {
        var recorder = kernel.Services.GetService(typeof(IAiUsageRecorder)) as IAiUsageRecorder;
        var descriptor = kernel.Services.GetService(typeof(AiKernelModelDescriptor)) as AiKernelModelDescriptor;
        if (recorder == null || descriptor == null)
            return Task.CompletedTask;

        return recorder.RecordFromMetadataAsync(
            metadata,
            operationType,
            descriptor.ModelId,
            elapsedMs,
            status,
            errorMessage);
    }
}
