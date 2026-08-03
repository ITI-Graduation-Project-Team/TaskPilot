using Microsoft.SemanticKernel;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IAiKernelService
    {
        Kernel CreateKernel(
            string modelId,
            string? httpClientName = null);
        Kernel CreateGeminiKernel(
            string modelId);
    }
}
