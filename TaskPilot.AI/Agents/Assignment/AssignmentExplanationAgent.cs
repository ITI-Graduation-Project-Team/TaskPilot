using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.AI.Constants;

namespace TaskPilot.AI.Agents.Assignment;

public interface IAssignmentExplanationAgent
{
    Task<Result<List<(string EmployeeId, string ReasonEn, string ReasonAr)>>> GenerateExplanationsAsync(ExplanationContextDto context, Guid projectId);
}

public class AssignmentExplanationAgent : IAssignmentExplanationAgent
{
    private readonly IAiKernelService _kernelService;
    private readonly IPromptLoaderService _promptLoader;

    public AssignmentExplanationAgent(
        IAiKernelService kernelService,
        IPromptLoaderService promptLoader)
    {
        _kernelService = kernelService;
        _promptLoader = promptLoader;
    }

    public async Task<Result<List<(string EmployeeId, string ReasonEn, string ReasonAr)>>> GenerateExplanationsAsync(ExplanationContextDto context, Guid projectId)
    {
        var fallbackReasons = context.TopDevelopers.Select(d => (
            EmployeeId: d.EmployeeId.ToString(),
            ReasonEn: $"Recommended as a suitable {d.JobTitle} for this task.",
            ReasonAr: $"تم التوصية به كـ {d.JobTitle} مناسب لهذه المهمة."
        )).ToList();

        try
        {
            var promptTemplate = await _promptLoader.LoadAsync("Assignment/ExplanationPrompt.yaml");
            var function = KernelFunctionYaml.FromPromptYaml(promptTemplate);
            var kernel = _kernelService.CreateKernel(ModelConstants.CheapModel); // Or use ModelConstants
            
            var arguments = new KernelArguments
            {
                ["TaskTitle"] = context.TaskTitle,
                ["TaskEstimatedHours"] = context.TaskEstimatedHours.ToString(),
                ["RequiredSkills"] = JsonSerializer.Serialize(context.RequiredSkills),
                ["Developers"] = JsonSerializer.Serialize(context.TopDevelopers),
                ["projectId"] = projectId
            };

            var response = await kernel.InvokeAsync(function, arguments);
            var aiResponse = response.GetValue<string>();
            
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                return Result.Success(fallbackReasons);
            }

            var jsonContent = aiResponse.Trim();
            if (jsonContent.StartsWith("```json"))
            {
                jsonContent = jsonContent.Substring(7);
                if (jsonContent.EndsWith("```"))
                    jsonContent = jsonContent.Substring(0, jsonContent.Length - 3);
            }

            var explanations = JsonSerializer.Deserialize<List<ExplanationResponse>>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (explanations == null || explanations.Count != context.TopDevelopers.Count)
            {
                return Result.Success(fallbackReasons);
            }

            return Result.Success(explanations.Select(e => (e.EmployeeId, e.ReasonEn, e.ReasonAr)).ToList());
        }
        catch (JsonException)
        {
            return Result.Success(fallbackReasons);
        }
        catch (Exception)
        {
            return Result.Failure<List<(string EmployeeId, string ReasonEn, string ReasonAr)>>(AssignmentErrors.ExplanationGenerationFailed);
        }
    }

    private class ExplanationResponse
    {
        public string EmployeeId { get; set; } = string.Empty;
        public string ReasonEn { get; set; } = string.Empty;
        public string ReasonAr { get; set; } = string.Empty;
    }
}
