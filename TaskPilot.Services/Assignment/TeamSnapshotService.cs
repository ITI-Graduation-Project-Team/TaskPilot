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
    private readonly IRepository<SkillAlias> _skillAliasRepository;
    private readonly IRepository<Company> _companyRepository;
    private readonly ILogger<TeamSnapshotService> _logger;

    public TeamSnapshotService(
        IRepository<Project> projectRepository,
        IRepository<Sprint> sprintRepository,
        IRepository<ProjectEmployee> projectEmployeeRepository,
        IRepository<UserSkill> userSkillRepository,
        IRepository<TaskItem> taskRepository,
        IRepository<SkillAlias> skillAliasRepository,
        IRepository<Company> companyRepository,
        ILogger<TeamSnapshotService> logger)
    {
        _projectRepository = projectRepository;
        _sprintRepository = sprintRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
        _userSkillRepository = userSkillRepository;
        _taskRepository = taskRepository;
        _skillAliasRepository = skillAliasRepository;
        _companyRepository = companyRepository;
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

        var company = await _companyRepository.GetByIdAsync(project.CompanyId);
        if (company == null)
            return Result<SprintAssignmentSnapshotDto>.Failure(CommonErrors.NotFound("Company"));

        if (sprint.ProjectId != projectId)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintDoesNotBelongToProject);

        if (sprint.Status == SprintStatus.Cancelled)
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.SprintCancelled);

        var projectEmployees = await _projectEmployeeRepository.GetQueryable()
            .Include(pe => pe.Employee)
            .Where(pe => pe.ProjectId == projectId && !pe.Employee.IsDeactivated)
            .ToListAsync(cancellationToken);

        if (!projectEmployees.Any())
            return Result<SprintAssignmentSnapshotDto>.Failure(AssignmentErrors.NoProjectTeam);

        var sprintTasks = await _taskRepository.GetQueryable()
            .Include(t => t.RequiredSkills)
                .ThenInclude(rs => rs.Skill)
            .Where(t => t.SprintId == sprintId)
            .ToListAsync(cancellationToken);

        var requiredSkillIds = sprintTasks.SelectMany(t => t.RequiredSkills).Select(rs => rs.SkillId).Distinct().ToList();
        var skillAliases = await _skillAliasRepository.GetQueryable()
            .Where(a => requiredSkillIds.Contains(a.SkillId))
            .ToListAsync(cancellationToken);
        var aliasLookup = skillAliases.GroupBy(a => a.SkillId).ToDictionary(g => g.Key, g => g.Select(a => a.Alias).ToList());

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
                    Aliases = aliasLookup.ContainsKey(rs.SkillId) ? aliasLookup[rs.SkillId] : new List<string>()
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

            int sprintWorkingDays = CalculateWorkingDays(sprint.StartDate, sprint.EndDate, company.WorkingDaysMask);
            var maxSprintHours = (double)(company.WorkingHoursPerDay * sprintWorkingDays * (pe.AllocationPercentage / 100m) * (decimal)company.DefaultCapacityBufferPercentage);

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

    private int CalculateWorkingDays(DateTime start, DateTime end, int workingDaysMask)
    {
        if (start > end) return 0;
        
        DateTime firstDay = start.Date;
        DateTime lastDay = end.Date;
        
        int totalIntermediateDays = (lastDay - firstDay).Days - 1;
        int workingDays = 0;

        if (IsWorkingDay(firstDay.DayOfWeek, workingDaysMask)) workingDays++;
        if (firstDay != lastDay && IsWorkingDay(lastDay.DayOfWeek, workingDaysMask)) workingDays++;

        if (totalIntermediateDays > 0)
        {
            int fullWeeks = totalIntermediateDays / 7;
            int remainingDays = totalIntermediateDays % 7;
            
            int workingDaysInWeek = 0;
            for (int i = 0; i < 7; i++)
            {
                if ((workingDaysMask & (1 << i)) != 0) workingDaysInWeek++;
            }
            
            workingDays += fullWeeks * workingDaysInWeek;
            
            DateTime currentDay = firstDay.AddDays(1);
            for (int i = 0; i < remainingDays; i++)
            {
                if (IsWorkingDay(currentDay.DayOfWeek, workingDaysMask))
                {
                    workingDays++;
                }
                currentDay = currentDay.AddDays(1);
            }
        }
        
        return workingDays;
    }

    private bool IsWorkingDay(DayOfWeek day, int mask)
    {
        return (mask & (1 << (int)day)) != 0;
    }
}
