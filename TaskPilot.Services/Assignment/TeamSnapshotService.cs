using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Helpers;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

namespace TaskPilot.Services.Assignment;

public class TeamSnapshotService : ITeamSnapshotService
{
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<Sprint> _sprintRepository;
    private readonly IRepository<ProjectEmployee> _projectEmployeeRepository;
    private readonly IRepository<UserSkill> _userSkillRepository;
    private readonly IRepository<TaskItem> _taskRepository;
    private readonly ILogger<TeamSnapshotService> _logger;

    public TeamSnapshotService(
        IRepository<Project> projectRepository,
        IRepository<Sprint> sprintRepository,
        IRepository<ProjectEmployee> projectEmployeeRepository,
        IRepository<UserSkill> userSkillRepository,
        IRepository<TaskItem> taskRepository,
        ILogger<TeamSnapshotService> logger)
    {
        _projectRepository = projectRepository;
        _sprintRepository = sprintRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
        _userSkillRepository = userSkillRepository;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<SprintAssignmentSnapshotDto>> GetSnapshotAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.ProjectNotFound);

        var sprint = await _sprintRepository.GetByIdAsync(sprintId);
        if (sprint == null)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintNotFound);

        if (sprint.ProjectId != projectId)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintDoesNotBelongToProject);

        if (sprint.Status == SprintStatus.Cancelled)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintCancelled);

        var projectEmployees = await _projectEmployeeRepository.GetQueryable()
            .Include(pe => pe.Employee)
            .Where(pe => pe.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        if (!projectEmployees.Any())
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.NoProjectTeam);

        var sprintTasks = await _taskRepository.GetQueryable()
            .Include(t => t.RequiredSkills)
                .ThenInclude(rs => rs.Skill)
            .Where(t => t.SprintId == sprintId)
            .ToListAsync(cancellationToken);

        var unassignedTasks = sprintTasks
            .Where(t => t.EmployeeId == null)
            .Select(t => new TaskSnapshotDto
            {
                TaskId = t.Id,
                TitleEn = t.TitleEn,
                TitleAr = t.TitleAr,
                EstimatedHours = t.EstimatedHours,
                Priority = t.Priority,
                EffortSize = t.EffortSize,
                Type = t.Type,
                RequiredSkills = t.RequiredSkills.Select(rs => new TaskRequiredSkillDto
                {
                    SkillId = rs.SkillId,
                    SkillName = rs.Skill?.Name ?? string.Empty,
                    RequiredLevel = rs.RequiredLevel
                }).ToList()
            }).ToList();

        var teamSnapshot = new TeamSnapshotDto
        {
            ProjectId = projectId,
            SprintId = sprintId
        };

        var employeeIds = projectEmployees.Select(pe => pe.EmployeeId).ToList();

        var activeProjectsCounts = await _projectEmployeeRepository.GetQueryable()
            .Where(pe => employeeIds.Contains(pe.EmployeeId))
            .GroupBy(pe => pe.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.Count, cancellationToken);

        var userSkills = await _userSkillRepository.GetQueryable()
            .Include(us => us.Skill)
            .Where(us => employeeIds.Contains(us.UserId))
            .ToListAsync(cancellationToken);

        var allAssignedTasks = await _taskRepository.GetQueryable()
            .Where(t => t.EmployeeId != null && employeeIds.Contains(t.EmployeeId.Value))
            .ToListAsync(cancellationToken);

        var completedSprints = await _sprintRepository.GetQueryable()
            .Where(s => s.ProjectId == projectId && s.Status == SprintStatus.Completed)
            .OrderByDescending(s => s.EndDate)
            .Take(3)
            .ToListAsync(cancellationToken);

        var completedSprintIds = completedSprints.Select(s => s.Id).ToList();

        var completedSprintTasks = await _taskRepository.GetQueryable()
            .Where(t => t.EmployeeId != null && 
                        employeeIds.Contains(t.EmployeeId.Value) && 
                        t.SprintId != null && 
                        completedSprintIds.Contains(t.SprintId.Value))
            .ToListAsync(cancellationToken);

        foreach (var pe in projectEmployees)
        {
            var empId = pe.EmployeeId;

            var empSkills = userSkills.Where(us => us.UserId == empId)
                .Select(us => new DeveloperSkillDto
                {
                    SkillId = us.SkillId,
                    SkillName = us.Skill?.Name ?? string.Empty,
                    Level = us.Level,
                    YearsOfExperience = (int)(us.YearsOfExperience ?? 0),
                    IsPrimary = us.IsPrimary
                }).ToList();

            var maxSprintHours = project.SprintDurationInDays * 6.0;

            var currentAssignedHours = (double)allAssignedTasks
                .Where(t => t.EmployeeId == empId && t.SprintId == sprintId)
                .Sum(t => t.EstimatedHours);

            var remainingHours = maxSprintHours - currentAssignedHours;

            var workloadPercentage = maxSprintHours > 0 ? (currentAssignedHours / maxSprintHours) * 100 : 0;

            if (remainingHours < 0 || workloadPercentage < 0 || workloadPercentage > 100)
            {
                return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.InvalidAvailabilityState);
            }

            var availabilityStatus = ComputeAvailabilityStatus(workloadPercentage);

            var activeTasksCount = allAssignedTasks.Count(t => t.EmployeeId == empId && t.Status != TaskItemStatus.Done);

            double? historicalVelocity = null;
            bool hasHistoricalData = false;
            int completedSprintsCount = 0;

            var empCompletedTasks = completedSprintTasks.Where(t => t.EmployeeId == empId).ToList();
            if (empCompletedTasks.Any())
            {
                var sprintVelocities = new List<double>();
                foreach (var s in completedSprints)
                {
                    var sTasks = empCompletedTasks
                        .Where(t => t.SprintId == s.Id && t.EstimatedHours > 0 && t.ActualHours > 0)
                        .ToList();
                    
                    if (sTasks.Any())
                    {
                        var avgVelocity = sTasks.Average(t => (double)(t.ActualHours / t.EstimatedHours));
                        sprintVelocities.Add(avgVelocity);
                    }
                }

                if (sprintVelocities.Any())
                {
                    hasHistoricalData = true;
                    completedSprintsCount = sprintVelocities.Count;
                    historicalVelocity = Math.Round(sprintVelocities.Average(), 2);
                }
            }

            teamSnapshot.Developers.Add(new DeveloperSnapshotDto
            {
                EmployeeId = empId,
                FullName = pe.Employee.FirstNameEn + " " + pe.Employee.LastNameEn,
                ProjectRole = pe.Role,
                SeniorityLevel = pe.Employee.SeniorityLevel ?? SeniorityLevel.Junior,
                AvailabilityStatus = availabilityStatus,
                Skills = empSkills,
                MaxSprintHours = maxSprintHours,
                CurrentAssignedHours = currentAssignedHours,
                RemainingHours = remainingHours,
                WorkloadPercentage = workloadPercentage,
                HistoricalVelocity = historicalVelocity,
                HasHistoricalData = hasHistoricalData,
                CompletedSprintsCount = completedSprintsCount,
                ActiveTasksCount = activeTasksCount
            });
        }

        teamSnapshot.TeamSize = teamSnapshot.Developers.Count;
        teamSnapshot.TotalTeamRemainingHours = teamSnapshot.Developers.Sum(d => d.RemainingHours);

        var result = new SprintAssignmentSnapshotDto
        {
            Team = teamSnapshot,
            UnassignedTasks = unassignedTasks
        };

        _logger.LogInformation(
            "Snapshot built for ProjectId: {ProjectId}, SprintId: {SprintId}. Developers: {DeveloperCount}, Unassigned Tasks: {UnassignedTasksCount}",
            projectId, sprintId, teamSnapshot.Developers.Count, unassignedTasks.Count);

        return Result<SprintAssignmentSnapshotDto>.Success(result);
    }

    private EmployeeAvailabilityStatus ComputeAvailabilityStatus(double workloadPercentage)
    {
        if (workloadPercentage <= 30) return EmployeeAvailabilityStatus.Available;
        if (workloadPercentage <= 70) return EmployeeAvailabilityStatus.PartiallyBusy;
        if (workloadPercentage <= 90) return EmployeeAvailabilityStatus.Busy;
        return EmployeeAvailabilityStatus.Overloaded;
    }
}
