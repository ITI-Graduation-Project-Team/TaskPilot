using TaskPilot.DTOs.Assignment;
using TaskPilot.Models.Common;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TaskPilot.Services.Assignment;

public class TeamSnapshotService : ITeamSnapshotService
{
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<Sprint> _sprintRepository;
    private readonly IRepository<ProjectEmployee> _projectEmployeeRepository;
    private readonly IRepository<TaskItem> _taskRepository;
    private readonly ILogger<TeamSnapshotService> _logger;

    public TeamSnapshotService(
        IRepository<Project> projectRepository,
        IRepository<Sprint> sprintRepository,
        IRepository<ProjectEmployee> projectEmployeeRepository,
        IRepository<TaskItem> taskRepository,
        ILogger<TeamSnapshotService> logger)
    {
        _projectRepository = projectRepository;
        _sprintRepository = sprintRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task<Result<SprintAssignmentSnapshotDto>> GetSnapshotAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var project = await _projectRepository.GetQueryable()
            .AsNoTracking()
            .Include(p => p.Company)
            .Include(p => p.Sprints.Where(s => s.Id == sprintId))
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken);
        var projectQueryMs = stopwatch.ElapsedMilliseconds;
        if (project == null)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.ProjectNotFound);

        var sprint = project.Sprints.FirstOrDefault();
        if (sprint == null)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintNotFound);

        if (sprint.ProjectId != projectId)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintDoesNotBelongToProject);

        if (sprint.Status == SprintStatus.Cancelled)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintCancelled);

        var projectEmployees = await _projectEmployeeRepository.GetQueryable()
            .AsNoTracking()
            .Include(pe => pe.Employee)
                .ThenInclude(e => e.UserSkills)
                    .ThenInclude(us => us.Skill)
            .Where(pe => pe.ProjectId == projectId && pe.IsActive && !pe.Employee.IsDeactivated)
            .ToListAsync(cancellationToken);
        var employeeQueryMs = stopwatch.ElapsedMilliseconds - projectQueryMs;

        // We no longer fail here if the project has no team.
        // It will be handled gracefully by the capacity validation (as a warning) and the UI will show 0 developers.
        var sprintTasks = await _taskRepository.GetQueryable()
            .AsNoTracking()
            .Include(t => t.RequiredSkills)
                .ThenInclude(rs => rs.Skill)
                    .ThenInclude(skill => skill.Aliases)
            .Where(t => t.SprintId == sprintId)
            .ToListAsync(cancellationToken);
        var taskQueryMs = stopwatch.ElapsedMilliseconds - projectQueryMs - employeeQueryMs;

        var unassignedTasks = sprintTasks
            .Where(t => t.Status != TaskItemStatus.Done)
            .Select(t => new TaskSnapshotDto
            {
                TaskId = t.Id,
                TitleEn = t.TitleEn,
                TitleAr = t.TitleAr,
                EstimatedHours = t.EstimatedHours,
                Priority = t.Priority,
                EffortSize = t.EffortSize,
                Type = t.Type,
                AssigneeId = t.EmployeeId,
                RequiredSkills = t.RequiredSkills.Select(rs => new TaskRequiredSkillDto
                {
                    SkillId = rs.SkillId,
                    SkillName = rs.Skill?.Name ?? string.Empty,
                    RequiredLevel = rs.RequiredLevel,
                    Aliases = rs.Skill?.Aliases.Select(a => a.Alias).ToList() ?? new List<string>()
                }).ToList()
            }).ToList();

        var teamSnapshot = new TeamSnapshotDto
        {
            ProjectId = projectId,
            SprintId = sprintId
        };

        var employeeIds = projectEmployees.Select(pe => pe.EmployeeId).ToList();
        var recentCompletedSprintIds = _sprintRepository.GetQueryable()
            .Where(s => s.ProjectId == projectId && s.Status == SprintStatus.Completed)
            .OrderByDescending(s => s.EndDate)
            .Take(3)
            .Select(s => s.Id);

        var completedTaskStats = await _taskRepository.GetQueryable()
            .AsNoTracking()
            .Where(t => t.EmployeeId != null &&
                        employeeIds.Contains(t.EmployeeId.Value) &&
                        t.SprintId != null &&
                        recentCompletedSprintIds.Contains(t.SprintId.Value) &&
                        t.EstimatedHours > 0 &&
                        t.ActualHours > 0)
            .GroupBy(t => t.EmployeeId!.Value)
            .Select(g => new
            {
                EmployeeId = g.Key,
                TotalEstimatedHours = g.Sum(t => t.EstimatedHours),
                TotalAbsoluteDeviation = g.Sum(t => Math.Abs(t.ActualHours - t.EstimatedHours)),
                CompletedSprintsCount = g.Select(t => t.SprintId).Distinct().Count()
            })
            .ToDictionaryAsync(x => x.EmployeeId, cancellationToken);
        var historyQueryMs = stopwatch.ElapsedMilliseconds - projectQueryMs - employeeQueryMs - taskQueryMs;

        foreach (var pe in projectEmployees)
        {
            var empId = pe.EmployeeId;

            var empSkills = pe.Employee.UserSkills
                .Select(us => new DeveloperSkillDto
                {
                    SkillId = us.SkillId,
                    SkillName = us.Skill?.Name ?? string.Empty,
                    Level = us.Level,
                    YearsOfExperience = (int)(us.YearsOfExperience ?? 0),
                    IsPrimary = us.IsPrimary
                }).ToList();

            var maxSprintHours = AssignmentCapacityCalculator.CalculateMaxSprintHours(
                sprint,
                project.Company,
                pe.AllocationPercentage);

            var currentAssignedHours = (double)sprintTasks
                .Where(t => t.EmployeeId == empId)
                .Sum(t => t.EstimatedHours);

            var remainingHours = maxSprintHours - currentAssignedHours;

            var workloadPercentage = maxSprintHours > 0 ? (currentAssignedHours / maxSprintHours) * 100 : 0;

            if (workloadPercentage < 0)
            {
                _logger.LogWarning("Negative workload percentage detected for employee {EmployeeId} in sprint {SprintId}: {WorkloadPercentage}%. Clamping to 0.", empId, sprintId, workloadPercentage);
                workloadPercentage = 0;
            }

            var availabilityStatus = ComputeAvailabilityStatus(workloadPercentage);

            double? historicalVelocity = null;
            bool hasHistoricalData = false;
            int completedSprintsCount = 0;

            if (completedTaskStats.TryGetValue(empId, out var stats) && stats.TotalEstimatedHours > 0)
            {
                hasHistoricalData = true;
                completedSprintsCount = stats.CompletedSprintsCount;
                historicalVelocity = Math.Round(
                    1 - (double)(stats.TotalAbsoluteDeviation / stats.TotalEstimatedHours),
                    2);
            }

            teamSnapshot.Developers.Add(new DeveloperSnapshotDto
            {
                EmployeeId = empId,
                FullName = pe.Employee.FirstNameEn + " " + pe.Employee.LastNameEn,
                JobTitle = pe.Employee.JobTitle ?? string.Empty,
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
                ActiveTasksCount = 0
            });
        }

        teamSnapshot.TeamSize = teamSnapshot.Developers.Count;
        teamSnapshot.TotalTeamRemainingHours = teamSnapshot.Developers.Sum(d => d.RemainingHours);

        var result = new SprintAssignmentSnapshotDto
        {
            SprintStatus = sprint.Status,
            Team = teamSnapshot,
            UnassignedTasks = unassignedTasks
        };

        _logger.LogInformation(
            "Assignment snapshot built for ProjectId: {ProjectId}, SprintId: {SprintId}. Developers: {DeveloperCount}, Tasks: {TaskCount}, ProjectQueryMs: {ProjectQueryMs}, EmployeeQueryMs: {EmployeeQueryMs}, TaskQueryMs: {TaskQueryMs}, HistoryQueryMs: {HistoryQueryMs}, MappingMs: {MappingMs}, DurationMs: {DurationMs}",
            projectId,
            sprintId,
            teamSnapshot.Developers.Count,
            unassignedTasks.Count,
            projectQueryMs,
            employeeQueryMs,
            taskQueryMs,
            historyQueryMs,
            stopwatch.ElapsedMilliseconds - projectQueryMs - employeeQueryMs - taskQueryMs - historyQueryMs,
            stopwatch.ElapsedMilliseconds);

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
