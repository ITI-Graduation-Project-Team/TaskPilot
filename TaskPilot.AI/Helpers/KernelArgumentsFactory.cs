// TODO: Audit all agents that perform structured scoring or 
// extraction (not conversational responses) and confirm they 
// use CreateDeterministicArguments() rather than new KernelArguments().
// RequirementAnalysisAgent was the first confirmed case — others 
// may carry the same non-determinism risk.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TaskPilot.AI.Helpers
{
    public static class KernelArgumentsFactory
    {
        public static KernelArguments
            CreateDeterministicArguments()
        {
            return new(
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.1,
                    TopP = 0.8
                });
        }

        public static KernelArguments
            CreateBalancedArguments()
        {
            return new(
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.3,
                    TopP = 0.9
                });
        }

        public static KernelArguments
            CreateCreativeArguments()
        {
            return new(
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.7,
                    TopP = 1
                });
        }
    }
}