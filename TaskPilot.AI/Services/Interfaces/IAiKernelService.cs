using Microsoft.SemanticKernel;

namespace TaskPilot.AI.Services.Interfaces
{
    public interface IAiKernelService
    {
        Kernel CreateKernel(
            string modelId);
        Kernel CreateGeminiKernel(
            string modelId);
    }
}
