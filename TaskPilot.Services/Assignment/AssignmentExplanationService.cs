using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.AI.Agents.Assignment;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TaskPilot.Models.Common;

namespace TaskPilot.Services.Assignment;

public class AssignmentExplanationService : IAssignmentExplanationService
{
    private readonly IAssignmentScoringService _scoringService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly AssignmentOptions _options;

    public AssignmentExplanationService(
        IAssignmentScoringService scoringService,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AssignmentOptions> options)
    {
        _scoringService = scoringService;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options.Value;
    }

    public async Task<Result<ExplainedAssignmentDto>> GenerateAsync(
        Guid projectId,
        Guid sprintId,
        string language,
        CancellationToken cancellationToken)
    {
        var scoringResult = await _scoringService.ScoreAsync(projectId, sprintId, cancellationToken);
        if (scoringResult.IsFailure)
        {
            return Result.Failure<ExplainedAssignmentDto>(scoringResult.Error!);
        }

        var scoredAssignment = scoringResult.Value;
        if (scoredAssignment == null || scoredAssignment.TaskScores == null)
        {
            return Result.Failure<ExplainedAssignmentDto>(AssignmentErrors.InvalidExplanationInput);
        }

        var explainedAssignment = new ExplainedAssignmentDto
        {
            ProjectId = scoredAssignment.ProjectId,
            SprintId = scoredAssignment.SprintId,
            TaskScores = new List<ExplainedTaskScoringResultDto>()
        };

        int maxConcurrency = _options.MaxExplanationConcurrency > 0 ? _options.MaxExplanationConcurrency : 5;
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var explanationTasks = scoredAssignment.TaskScores.Select(async taskScore =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var explainedTaskScore = new ExplainedTaskScoringResultDto
                {
                    Task = taskScore.Task,
                    IsUnassignable = taskScore.IsUnassignable,
                    RankedDevelopers = new List<ExplainedDeveloperDto>()
                };

                var topDevelopers = taskScore.RankedDevelopers.Take(3).ToList();
                var context = new ExplanationContextDto
                {
                    TaskTitle = taskScore.Task.TitleEn,
                    TaskEstimatedHours = taskScore.Task.EstimatedHours,
                    RequiredSkills = taskScore.Task.RequiredSkills,
                    TopDevelopers = topDevelopers.Select(d => new DeveloperScoreContext
                    {
                        EmployeeId = d.EmployeeId,
                        FullName = d.FullName,
                        JobTitle = d.JobTitle,
                        FinalScore = d.FinalScore,
                        SkillScore = d.SkillScore,
                        AvailabilityScore = d.AvailabilityScore,
                        VelocityScore = d.VelocityScore,
                        ExperienceScore = d.ExperienceScore,
                        RemainingHours = d.RemainingHours,
                        HasSufficientCapacity = d.HasSufficientCapacity,
                        SkillGaps = d.SkillGaps
                    }).ToList(),
                    Language = language
                };

                List<(string EmployeeId, string ReasonEn, string ReasonAr)> reasons = new();
                if (topDevelopers.Count > 0)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var explanationAgent = scope.ServiceProvider.GetRequiredService<IAssignmentExplanationAgent>();
                    
                    var explanationResult = await explanationAgent.GenerateExplanationsAsync(context);
                    if (explanationResult.IsSuccess && explanationResult.Value != null)
                    {
                        reasons = explanationResult.Value;
                    }
                }

                for (int i = 0; i < taskScore.RankedDevelopers.Count; i++)
                {
                    var developer = taskScore.RankedDevelopers[i];
                    var devReason = reasons.FirstOrDefault(r => r.EmployeeId == developer.EmployeeId.ToString());
                    var explainedDeveloper = new ExplainedDeveloperDto
                    {
                        EmployeeId = developer.EmployeeId,
                        FullName = developer.FullName,
                        JobTitle = developer.JobTitle,
                        FinalScore = developer.FinalScore,
                        SkillScore = developer.SkillScore,
                        AvailabilityScore = developer.AvailabilityScore,
                        VelocityScore = developer.VelocityScore,
                        ExperienceScore = developer.ExperienceScore,
                        SkillGaps = developer.SkillGaps,
                        RemainingHours = developer.RemainingHours,
                        MaxSprintHours = developer.MaxSprintHours,
                        CurrentAssignedHours = developer.CurrentAssignedHours,
                        HasSufficientCapacity = developer.HasSufficientCapacity,
                        ReasonEn = !string.IsNullOrEmpty(devReason.ReasonEn) ? devReason.ReasonEn : "Explanation not generated (not in top 3).",
                        ReasonAr = !string.IsNullOrEmpty(devReason.ReasonAr) ? devReason.ReasonAr : "لم يتم إنشاء التفسير (ليس ضمن أفضل 3)."
                    };
                    explainedTaskScore.RankedDevelopers.Add(explainedDeveloper);
                }

                return explainedTaskScore;
            }
            catch (Exception)
            {
                // Fallback isolation
                var fallbackTaskScore = new ExplainedTaskScoringResultDto
                {
                    Task = taskScore.Task,
                    IsUnassignable = taskScore.IsUnassignable,
                    RankedDevelopers = new List<ExplainedDeveloperDto>()
                };

                for (int i = 0; i < taskScore.RankedDevelopers.Count; i++)
                {
                    var developer = taskScore.RankedDevelopers[i];
                    fallbackTaskScore.RankedDevelopers.Add(new ExplainedDeveloperDto
                    {
                        EmployeeId = developer.EmployeeId,
                        FullName = developer.FullName,
                        JobTitle = developer.JobTitle,
                        FinalScore = developer.FinalScore,
                        SkillScore = developer.SkillScore,
                        AvailabilityScore = developer.AvailabilityScore,
                        VelocityScore = developer.VelocityScore,
                        ExperienceScore = developer.ExperienceScore,
                        SkillGaps = developer.SkillGaps,
                        RemainingHours = developer.RemainingHours,
                        MaxSprintHours = developer.MaxSprintHours,
                        CurrentAssignedHours = developer.CurrentAssignedHours,
                        HasSufficientCapacity = developer.HasSufficientCapacity,
                        ReasonEn = "Explanation generation failed.",
                        ReasonAr = "فشل إنشاء التفسير."
                    });
                }
                return fallbackTaskScore;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var explainedTaskScores = await Task.WhenAll(explanationTasks);
        explainedAssignment.TaskScores = explainedTaskScores.ToList();

        return Result.Success(explainedAssignment);
    }
}

