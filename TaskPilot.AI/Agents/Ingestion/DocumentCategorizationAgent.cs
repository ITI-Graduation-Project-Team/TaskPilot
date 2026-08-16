using Microsoft.SemanticKernel;
using TaskPilot.AI.Constants;
using TaskPilot.AI.Enums;
using TaskPilot.AI.Helpers;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Agents.Ingestion
{
    public class DocumentCategorizationAgent
    {
        private readonly IAiKernelService _kernelService;
        private readonly IPromptLoaderService _promptLoader;

        public DocumentCategorizationAgent(
            IAiKernelService kernelService,
            IPromptLoaderService promptLoader)
        {
            _kernelService = kernelService;
            _promptLoader = promptLoader;
        }

        public async Task<DocumentCategory> CategorizeAsync(
            string fileName,
            string extractedText,
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel);
                var prompt = await _promptLoader.LoadAsync("Requirements/Categorization.yaml");
                var function = KernelFunctionYaml.FromPromptYaml(prompt);

                var arguments = KernelArgumentsFactory.CreateDeterministicArguments();
                arguments["fileName"] = fileName;
                arguments["extractedText"] = extractedText;
                arguments["projectId"] = projectId;

                var result = await kernel.InvokeAsync(function, arguments);
                var response = result.ToString().Trim();

                if (Enum.TryParse<DocumentCategory>(response, true, out var category))
                {
                    return category;
                }
            }
            catch
            {
                // Fallback to basic rule-based classification if AI call fails
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();

            if (extension == ".mp3" || extension == ".wav" || extension == ".m4a")
            {
                return DocumentCategory.AudioTranscript;
            }

            if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
            {
                return DocumentCategory.Image;
            }

            if (extension == ".pdf" || extension == ".docx" || extension == ".txt" || extension == ".md")
            {
                if (name.Contains("meeting") || name.Contains("minutes") || name.Contains("notes"))
                {
                    return DocumentCategory.MeetingNotes;
                }
                if (name.Contains("api") || name.Contains("swagger") || name.Contains("endpoint"))
                {
                    return DocumentCategory.ApiDocumentation;
                }
                if (name.Contains("architecture") || name.Contains("design") || name.Contains("system"))
                {
                    return DocumentCategory.Architecture;
                }
                if (name.Contains("requirement") || name.Contains("spec") || name.Contains("prd"))
                {
                    return DocumentCategory.Requirements;
                }
            }

            return DocumentCategory.Uncategorized;
        }
    }
}
