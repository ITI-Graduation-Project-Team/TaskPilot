using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.AI.Agents.Assignment;

namespace TaskPilot.Services.Assignment;

public class AssignmentExplanationService : IAssignmentExplanationService
{
    private readonly IAssignmentScoringService _scoringService;
    private readonly IAssignmentExplanationAgent _explanationAgent;

    public AssignmentExplanationService(
        IAssignmentScoringService scoringService,
        IAssignmentExplanationAgent explanationAgent)
    {
        _scoringService = scoringService;
        _explanationAgent = explanationAgent;
    }

    public async Task<Result<ExplainedAssignmentDto>> GenerateAsync(
        Guid projectId,
        Guid sprintId,
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

        foreach (var taskScore in scoredAssignment.TaskScores)
        {
            var explainedTaskScore = new ExplainedTaskScoringResultDto
            {
                Task = taskScore.Task,
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
                }).ToList()
            };

            List<(string ReasonEn, string ReasonAr)> reasons = new();
            if (topDevelopers.Count > 0)
            {
                var explanationResult = await _explanationAgent.GenerateExplanationsAsync(context);
                if (explanationResult.IsFailure)
                {
                    return Result.Failure<ExplainedAssignmentDto>(explanationResult.Error!);
                }
                reasons = explanationResult.Value!;
            }

            for (int i = 0; i < taskScore.RankedDevelopers.Count; i++)
            {
                var developer = taskScore.RankedDevelopers[i];
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
                    HasSufficientCapacity = developer.HasSufficientCapacity,
                    ReasonEn = i < 3 && i < reasons.Count ? reasons[i].ReasonEn : "Explanation not generated (not in top 3).",
                    ReasonAr = i < 3 && i < reasons.Count ? reasons[i].ReasonAr : "لم يتم إنشاء التفسير (ليس ضمن أفضل 3)."
                };
                explainedTaskScore.RankedDevelopers.Add(explainedDeveloper);
            }

            explainedAssignment.TaskScores.Add(explainedTaskScore);
        }

        return Result.Success(explainedAssignment);
    }
}
