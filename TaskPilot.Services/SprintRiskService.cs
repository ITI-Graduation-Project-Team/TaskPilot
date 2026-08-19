using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.AI.Agents.Sprint;
using TaskPilot.AI.Models.Sprint;
using TaskPilot.Data.Context;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SprintRiskService : ISprintRiskService
    {
        private readonly ApplicationDbContext _context;
        private readonly SprintRiskDetectionAgent _detectionAgent;
        private readonly SprintBurnoutAgent _burnoutAgent;
        private readonly WhatIfSimulationAgent _simulationAgent;
        private readonly INotificationService _notificationService;

        public SprintRiskService(
            ApplicationDbContext context,
            SprintRiskDetectionAgent detectionAgent,
            SprintBurnoutAgent burnoutAgent,
            WhatIfSimulationAgent simulationAgent,
            INotificationService notificationService)
        {
            _context = context;
            _detectionAgent = detectionAgent;
            _burnoutAgent = burnoutAgent;
            _simulationAgent = simulationAgent;
            _notificationService = notificationService;
        }

        public async Task DetectAndPersistRisksAsync(Guid sprintId, CancellationToken ct = default)
        {
            var sprint = await _context.Set<Sprint>()
                .Include(s => s.Project)
                .ThenInclude(p => p.ProjectEmployees)
                .ThenInclude(pe => pe.Employee)
                .Include(s => s.Tasks)
                .FirstOrDefaultAsync(s => s.Id == sprintId, ct);

            if (sprint == null || sprint.Status != SprintStatus.Active || sprint.IsDeleted)
                return;

            var activeTasks = sprint.Tasks.Where(t => t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress).ToList();

            int daysRemaining = (sprint.EndDate - DateTime.UtcNow).Days;
            
            if (daysRemaining < 0) return;
            if (!activeTasks.Any()) return;

            var context = new SprintRiskContext
            {
                SprintGoal = sprint.SprintGoalEn ?? "",
                DaysRemaining = daysRemaining,
                TotalWorkingDaysInSprint = (sprint.EndDate - sprint.StartDate).Days,
                Tasks = activeTasks.Select(t => new TaskRiskSnapshot
                {
                    TaskId = t.Id,
                    Title = t.TitleEn,
                    Status = t.Status.ToString(),
                    EstimatedHours = t.EstimatedHours,
                    ActualHours = t.ActualHours,
                    IsBlocked = false,
                    RequiredSkills = new List<string>(), 
                    AssignedEmployeeId = t.EmployeeId,
                    AssignedEmployeeName = "" 
                }).ToList(),
                TeamMembers = sprint.Project.ProjectEmployees.Select(pe => new TeamMemberSnapshot
                {
                    EmployeeId = pe.EmployeeId,
                    Name = pe.Employee.FirstNameEn + " " + pe.Employee.LastNameEn,
                    ScheduledHoursToday = 0, 
                    MaxSprintHours = pe.Employee.MaxSprintHours ?? 40,
                    Skills = new List<string>()
                }).ToList()
            };

            var detectionResult = await _detectionAgent.DetectRisksAsync(context, ct);

            if (detectionResult?.Risks == null) return;

            foreach (var risk in detectionResult.Risks)
            {
                if (!Enum.TryParse<SprintRiskType>(risk.RiskType, true, out var riskType)) continue;
                if (!Enum.TryParse<RiskSeverity>(risk.Severity, true, out var severity)) continue;

                var existingAlert = await _context.Set<SprintRiskAlert>()
                    .FirstOrDefaultAsync(a => 
                        a.SprintId == sprintId && 
                        a.RiskType == riskType && 
                        a.AffectedTaskId == risk.AffectedTaskId && 
                        a.LastDetectedAt >= DateTime.UtcNow.AddHours(-23), ct);

                if (existingAlert != null)
                {
                    existingAlert.LastDetectedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.Set<SprintRiskAlert>().Add(new SprintRiskAlert
                    {
                        SprintId = sprintId,
                        RiskType = riskType,
                        Severity = severity,
                        AffectedTaskId = risk.AffectedTaskId,
                        AffectedEmployeeId = risk.AffectedEmployeeId,
                        MessageEn = risk.MessageEn,
                        MessageAr = risk.MessageAr,
                        LastDetectedAt = DateTime.UtcNow
                    });

                    if (sprint.Project.ManagerId != Guid.Empty)
                    {
                        await _notificationService.SendAsync(
                            userId: sprint.Project.ManagerId,
                            type: NotificationType.SprintRiskDetected,
                            messageEn: $"New sprint risk detected: {risk.MessageEn}",
                            messageAr: $"تم اكتشاف خطر جديد في السبرينت: {risk.MessageAr}",
                            url: $"/projects/{sprint.ProjectId}/sprints/{sprintId}/risks"
                        );
                    }
                }
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task<Result<List<SprintRiskAlertDto>>> GetAlertsAsync(Guid sprintId)
        {
            var alerts = await _context.Set<SprintRiskAlert>()
                .Include(a => a.AffectedTask)
                .Include(a => a.AffectedEmployee)
                .Where(a => a.SprintId == sprintId && !a.IsDismissed)
                .Select(a => new SprintRiskAlertDto
                {
                    Id = a.Id,
                    RiskType = a.RiskType.ToString(),
                    Severity = a.Severity.ToString(),
                    MessageEn = a.MessageEn,
                    MessageAr = a.MessageAr,
                    AffectedTaskId = a.AffectedTaskId,
                    AffectedTaskTitle = a.AffectedTask != null ? a.AffectedTask.TitleEn : null,
                    AffectedEmployeeId = a.AffectedEmployeeId,
                    AffectedEmployeeName = a.AffectedEmployee != null ? (a.AffectedEmployee.FirstNameEn + " " + a.AffectedEmployee.LastNameEn) : null,
                    DetectedAt = a.LastDetectedAt,
                    IsDismissed = a.IsDismissed
                })
                .ToListAsync();

            return Result<List<SprintRiskAlertDto>>.Success(alerts);
        }

        public async Task<Result> DismissAlertAsync(Guid alertId, Guid requestingUserId)
        {
            var alert = await _context.Set<SprintRiskAlert>()
                .Include(a => a.Sprint)
                .ThenInclude(s => s.Project)
                .FirstOrDefaultAsync(a => a.Id == alertId);

            if (alert == null) return Result.Failure(SprintRiskErrors.AlertNotFound);

            alert.IsDismissed = true;
            await _context.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result<SprintRiskSimulationResponseDto>> SimulateAsync(Guid alertId, CancellationToken ct = default)
        {
            var alert = await _context.Set<SprintRiskAlert>()
                .Include(a => a.Sprint)
                .ThenInclude(s => s.Project)
                .ThenInclude(p => p.ProjectEmployees)
                .ThenInclude(pe => pe.Employee)
                .Include(a => a.Sprint.Tasks)
                .FirstOrDefaultAsync(a => a.Id == alertId, ct);

            if (alert == null) return Result<SprintRiskSimulationResponseDto>.Failure(SprintRiskErrors.AlertNotFound);

            var activeTasks = alert.Sprint.Tasks.Where(t => t.Status == TaskItemStatus.ToDo || t.Status == TaskItemStatus.InProgress).ToList();
            int daysRemaining = (alert.Sprint.EndDate - DateTime.UtcNow).Days;

            var context = new SprintRiskContext
            {
                SprintGoal = alert.Sprint.SprintGoalEn ?? "",
                DaysRemaining = daysRemaining,
                TotalWorkingDaysInSprint = (alert.Sprint.EndDate - alert.Sprint.StartDate).Days,
                Tasks = activeTasks.Select(t => new TaskRiskSnapshot
                {
                    TaskId = t.Id,
                    Title = t.TitleEn,
                    Status = t.Status.ToString(),
                    EstimatedHours = t.EstimatedHours,
                    ActualHours = t.ActualHours,
                    IsBlocked = false,
                    RequiredSkills = new List<string>(),
                    AssignedEmployeeId = t.EmployeeId
                }).ToList(),
                TeamMembers = alert.Sprint.Project.ProjectEmployees.Select(pe => new TeamMemberSnapshot
                {
                    EmployeeId = pe.EmployeeId,
                    Name = pe.Employee.FirstNameEn + " " + pe.Employee.LastNameEn,
                    ScheduledHoursToday = 0,
                    MaxSprintHours = pe.Employee.MaxSprintHours ?? 40,
                    Skills = new List<string>()
                }).ToList()
            };

            var simulation = await _simulationAgent.SimulateAsync(alert, context, ct);

            if (simulation == null) return Result<SprintRiskSimulationResponseDto>.Failure(SprintRiskErrors.SimulationFailed);

            var response = new SprintRiskSimulationResponseDto
            {
                AlertId = alert.Id,
                Scenarios = simulation.Scenarios.Select(s => new WhatIfScenarioDto
                {
                    TitleEn = s.TitleEn,
                    TitleAr = s.TitleAr,
                    DescriptionEn = s.DescriptionEn,
                    DescriptionAr = s.DescriptionAr,
                    ProjectedImpactEn = s.ProjectedImpactEn,
                    ProjectedImpactAr = s.ProjectedImpactAr,
                    SuggestedAction = new WhatIfActionDto
                    {
                        ActionType = s.ActionType,
                        TargetTaskId = s.TargetTaskId,
                        SuggestedEmployeeId = s.SuggestedEmployeeId,
                        ExtensionDays = s.ExtensionDays,
                        StoryToDropId = s.StoryToDropId
                    }
                }).ToList()
            };

            return Result<SprintRiskSimulationResponseDto>.Success(response);
        }
        public async Task AnalyzeSprintBurnoutAsync(Guid sprintId, CancellationToken ct = default)
        {
            var sprint = await _context.Set<Sprint>()
                .Include(s => s.Project)
                .ThenInclude(p => p.ProjectEmployees)
                .ThenInclude(pe => pe.Employee)
                .Include(s => s.Tasks)
                .ThenInclude(t => t.Comments)
                .FirstOrDefaultAsync(s => s.Id == sprintId, ct);

            if (sprint == null || sprint.Status != SprintStatus.Active || sprint.IsDeleted)
                return;

            foreach (var pe in sprint.Project.ProjectEmployees)
            {
                var employee = pe.Employee;
                var assignedTasks = sprint.Tasks.Where(t => t.EmployeeId == employee.Id && !t.IsDeleted).ToList();
                var commentsCount = assignedTasks.SelectMany(t => t.Comments).Count(c => c.CreatedBy == employee.Id);

                var employeeContext = new EmployeeSprintBurnoutContext
                {
                    EmployeeId = employee.Id,
                    EmployeeName = $"{employee.FirstNameEn} {employee.LastNameEn}",
                    MaxSprintCapacity = employee.MaxSprintHours ?? 40,
                    AssignedHours = assignedTasks.Sum(t => t.EstimatedHours),
                    ActualHours = assignedTasks.Sum(t => t.ActualHours),
                    TasksAssigned = assignedTasks.Count,
                    TasksOverdue = assignedTasks.Count(t => t.Status != TaskItemStatus.Done && t.ActualHours > t.EstimatedHours),
                    CommentsMade = commentsCount,
                    StatusUpdates = assignedTasks.Count(t => t.Status == TaskItemStatus.Done || t.Status == TaskItemStatus.Review)
                };

                var burnoutResult = await _burnoutAgent.AnalyzeAsync(employeeContext, ct);

                var snapshot = new SprintBurnoutSnapshot
                {
                    SprintId = sprint.Id,
                    EmployeeId = employee.Id,
                    BurnoutScore = burnoutResult.BurnoutScore,
                    WorkloadScore = burnoutResult.WorkloadScore,
                    PaceScore = burnoutResult.PaceScore,
                    EngagementScore = burnoutResult.EngagementScore,
                    RiskLevel = burnoutResult.RiskLevel,
                    TrendDirection = burnoutResult.TrendDirection,
                    AnalyzedAt = DateTime.UtcNow
                };

                _context.Set<SprintBurnoutSnapshot>().Add(snapshot);
            }

            await _context.SaveChangesAsync(ct);
        }
        public async Task<Result<TeamPulseDto>> GetTeamPulseAsync(Guid sprintId, CancellationToken ct = default)
        {
            var sprint = await _context.Set<Sprint>()
                .AsNoTracking()
                .AsSplitQuery()
                .Include(s => s.Tasks)
                .Include(s => s.Project)
                .ThenInclude(p => p.Company)
                .Include(s => s.Project)
                .ThenInclude(p => p.ProjectEmployees)
                .ThenInclude(pe => pe.Employee)
                .FirstOrDefaultAsync(s => s.Id == sprintId, ct);
                
            if (sprint == null)
                return Result<TeamPulseDto>.Failure(SprintRiskErrors.SprintNotFound);

            var now = DateTime.UtcNow;
            var tasks = sprint.Tasks.Where(t => !t.IsDeleted).ToList();
            var unfinishedTasks = tasks.Where(t => t.Status != TaskItemStatus.Done).ToList();
            var activeMembers = sprint.Project.ProjectEmployees
                .Where(pe => pe.IsActive && !pe.Employee.IsDeactivated)
                .ToList();
            var allocationByEmployeeId = activeMembers.ToDictionary(
                pe => pe.EmployeeId,
                pe => Math.Clamp(pe.AllocationPercentage, 0, 100) / 100m);

            int totalTasks = tasks.Count;
            int completedTasks = tasks.Count(t => t.Status == TaskItemStatus.Done);
            int progressPercent = totalTasks == 0 ? 100 : (int)Math.Round((completedTasks * 100m) / totalTasks);
            var company = sprint.Project.Company;
            int workingDaysMask = company?.WorkingDaysMask ?? 62;
            decimal workingHoursPerDay = company?.WorkingHoursPerDay > 0 ? company.WorkingHoursPerDay : 8m;
            decimal capacityBuffer = company?.DefaultCapacityBufferPercentage > 0 ? company.DefaultCapacityBufferPercentage : 1m;

            int totalWorkingDays = Math.Max(1, CountWorkingDays(sprint.StartDate.Date, sprint.EndDate.Date, workingDaysMask));
            int elapsedWorkingDays = sprint.Status == SprintStatus.Completed
                ? totalWorkingDays
                : CountWorkingDays(sprint.StartDate.Date, now.Date > sprint.EndDate.Date ? sprint.EndDate.Date : now.Date, workingDaysMask);
            elapsedWorkingDays = Math.Clamp(elapsedWorkingDays, 0, totalWorkingDays);
            int timeUsedPercent = (int)Math.Round((elapsedWorkingDays * 100m) / totalWorkingDays);
            int workingDaysLeft = sprint.Status == SprintStatus.Completed
                ? 0
                : CountWorkingDays(now.Date > sprint.StartDate.Date ? now.Date : sprint.StartDate.Date, sprint.EndDate.Date, workingDaysMask);
            workingDaysLeft = Math.Clamp(workingDaysLeft, 0, totalWorkingDays);

            decimal GetTaskRemainingHours(TaskItem task)
            {
                if (task.Status == TaskItemStatus.Done) return 0;
                return task.EstimatedHours;
            }

            decimal remainingHours = unfinishedTasks.Sum(GetTaskRemainingHours);
            decimal totalEstimatedHours = tasks.Sum(t => t.EstimatedHours);
            decimal completedEstimatedHours = tasks
                .Where(t => t.Status == TaskItemStatus.Done)
                .Sum(t => t.EstimatedHours);
            int effortProgressPercent = totalEstimatedHours <= 0
                ? (totalTasks == 0 ? 100 : 0)
                : (int)Math.Round((completedEstimatedHours * 100m) / totalEstimatedHours);
            int unassignedHighPriorityCount = unfinishedTasks.Count(t =>
                !t.EmployeeId.HasValue && (t.Priority == TaskPriority.High || t.Priority == TaskPriority.Critical));

            decimal GetEffectiveActualHours(TaskItem task)
            {
                if (task.Status != TaskItemStatus.InProgress || !task.InProgressAt.HasValue)
                {
                    return task.ActualHours;
                }

                var allocationRatio = task.EmployeeId.HasValue && allocationByEmployeeId.TryGetValue(task.EmployeeId.Value, out var ratio)
                    ? ratio
                    : 1m;
                var runningHours = CalculateWorkingHours(task.InProgressAt.Value, now, workingDaysMask, workingHoursPerDay);
                return task.ActualHours + Math.Round(runningHours * allocationRatio, 2);
            }

            int estimateExceededCount = unfinishedTasks.Count(t => t.EstimatedHours > 0 && GetEffectiveActualHours(t) > t.EstimatedHours);
            int stuckTasksCount = unfinishedTasks.Count(t => IsTaskStuck(t, now, workingDaysMask, workingHoursPerDay));
            int reviewTasksCount = unfinishedTasks.Count(t => t.Status == TaskItemStatus.Review);
            decimal reviewEstimatedHours = unfinishedTasks
                .Where(t => t.Status == TaskItemStatus.Review)
                .Sum(t => t.EstimatedHours);

            var memberDtos = activeMembers.Select(pe =>
            {
                var emp = pe.Employee;
                decimal allocationRatio = Math.Clamp(pe.AllocationPercentage, 0, 100) / 100m;
                decimal availableRemaining = sprint.Status == SprintStatus.Completed
                    ? 0
                    : Math.Round(workingHoursPerDay * workingDaysLeft * allocationRatio * capacityBuffer, 1);
                decimal assignedRemaining = unfinishedTasks
                    .Where(t => t.EmployeeId == emp.Id)
                    .Sum(GetTaskRemainingHours);
                var assignedUnfinished = unfinishedTasks
                    .Where(t => t.EmployeeId == emp.Id)
                    .ToList();
                int memberStuckCount = assignedUnfinished.Count(t => IsTaskStuck(t, now, workingDaysMask, workingHoursPerDay));
                int memberEstimateExceededCount = assignedUnfinished.Count(t => t.EstimatedHours > 0 && GetEffectiveActualHours(t) > t.EstimatedHours);
                int memberHighPriorityCount = assignedUnfinished.Count(t => t.Priority == TaskPriority.High || t.Priority == TaskPriority.Critical);
                int memberReviewCount = assignedUnfinished.Count(t => t.Status == TaskItemStatus.Review);
                decimal completedByMember = tasks
                    .Where(t => t.EmployeeId == emp.Id && t.Status == TaskItemStatus.Done)
                    .Sum(t => t.EstimatedHours);
                int usagePercent = availableRemaining <= 0
                    ? (assignedRemaining > 0 ? 100 : 0)
                    : (int)Math.Round((assignedRemaining * 100m) / availableRemaining);
                string loadStatus = GetLoadStatus(assignedRemaining, availableRemaining, usagePercent);
                int workloadPressurePercent = CalculateWorkloadPressure(
                    usagePercent,
                    memberStuckCount,
                    memberEstimateExceededCount,
                    memberHighPriorityCount,
                    memberReviewCount);

                return new TeamPulseMemberDto
                {
                    EmployeeId = emp.Id,
                    Initials = GetInitials(emp.FirstNameEn, emp.LastNameEn),
                    Name = $"{emp.FirstNameEn} {emp.LastNameEn}",
                    JobTitle = emp.JobTitle ?? "Software Engineer",
                    RiskLevel = loadStatus,
                    BurnoutScore = workloadPressurePercent,
                    AssignedRemainingHours = Math.Round(assignedRemaining, 1),
                    AvailableRemainingHours = availableRemaining,
                    RemainingCapacityDeltaHours = Math.Round(availableRemaining - assignedRemaining, 1),
                    CompletedEstimatedHours = Math.Round(completedByMember, 1),
                    UsagePercent = usagePercent,
                    WorkloadPressurePercent = workloadPressurePercent,
                    ActiveTasksCount = assignedUnfinished.Count,
                    HighPriorityTasksCount = memberHighPriorityCount,
                    StuckTasksCount = memberStuckCount,
                    EstimateExceededTasksCount = memberEstimateExceededCount,
                    ReviewTasksCount = memberReviewCount,
                    LoadStatus = loadStatus,
                    RiskFactors = new RiskFactorsDto
                    {
                        Workload = Math.Clamp(usagePercent, 0, 100),
                        Pace = memberEstimateExceededCount > 0 ? 70 : 0,
                        Engagement = memberStuckCount > 0 ? 60 : 0
                    },
                    TrendDirection = "Stable",
                    History = new List<int> { workloadPressurePercent }
                };
            })
            .OrderByDescending(m => m.WorkloadPressurePercent)
            .ThenByDescending(m => m.UsagePercent)
            .ThenBy(m => m.Name)
            .ToList();

            int overloadedCount = memberDtos.Count(m => m.UsagePercent > 110);
            decimal teamRemainingCapacity = memberDtos.Sum(m => m.AvailableRemainingHours);
            decimal teamDailyCapacity = activeMembers.Sum(pe =>
                workingHoursPerDay * (Math.Clamp(pe.AllocationPercentage, 0, 100) / 100m) * capacityBuffer);
            decimal estimatedWorkingDaysNeeded = teamDailyCapacity <= 0 || remainingHours <= 0
                ? 0
                : Math.Round(remainingHours / teamDailyCapacity, 1);
            int capacityUsagePercent = teamRemainingCapacity <= 0
                ? (remainingHours > 0 ? 100 : 0)
                : (int)Math.Round((remainingHours * 100m) / teamRemainingCapacity);

            int scheduleGap = timeUsedPercent - effortProgressPercent;
            string deliveryStatus = GetDeliveryStatus(scheduleGap, capacityUsagePercent, overloadedCount, stuckTasksCount, workingDaysLeft, remainingHours);
            int healthScore = CalculateHealthScore(scheduleGap, capacityUsagePercent, overloadedCount, stuckTasksCount, estimateExceededCount);

            var needsAttention = BuildNeedsAttention(memberDtos, unfinishedTasks, scheduleGap, capacityUsagePercent, workingDaysLeft, remainingHours, reviewTasksCount, reviewEstimatedHours, GetEffectiveActualHours, now, workingDaysMask, workingHoursPerDay)
                .Take(6)
                .ToList();

            var risks = BuildRiskSummary(overloadedCount, scheduleGap, capacityUsagePercent, stuckTasksCount, estimateExceededCount, reviewTasksCount, reviewEstimatedHours, remainingHours);
            int highLoadAverage = memberDtos.Any()
                ? (int)Math.Round(memberDtos.Average(m => Math.Min(m.UsagePercent, 100)))
                : 0;

            var workloadDistribution = new WorkloadDistributionDto
            {
                Labels = memberDtos.Select(m => m.Name).ToList(),
                Series = memberDtos.Select(m => (int)Math.Round(m.AssignedRemainingHours)).ToList()
            };

            var teamPulse = new TeamPulseDto
            {
                TeamBurnoutRisk = highLoadAverage,
                Summary = new SprintHealthSummaryDto
                {
                    DeliveryStatus = deliveryStatus,
                    ProgressPercent = progressPercent,
                    EffortProgressPercent = effortProgressPercent,
                    DoneTasks = completedTasks,
                    TotalTasks = totalTasks,
                    CompletedEstimatedHours = (int)Math.Round(completedEstimatedHours),
                    TotalEstimatedHours = (int)Math.Round(totalEstimatedHours),
                    RemainingHours = (int)Math.Round(remainingHours),
                    WorkingDaysLeft = workingDaysLeft,
                    TeamRemainingCapacity = (int)Math.Round(teamRemainingCapacity),
                    CapacityUsagePercent = capacityUsagePercent,
                    EstimatedWorkingDaysNeeded = estimatedWorkingDaysNeeded,
                    SpareCapacityHours = (int)Math.Round(teamRemainingCapacity - remainingHours),
                    OverloadedCount = overloadedCount,
                    UnassignedHighPriorityCount = unassignedHighPriorityCount,
                    StuckTasksCount = stuckTasksCount,
                    EstimateExceededCount = estimateExceededCount,
                    ReviewTasksCount = reviewTasksCount
                },
                Kpis = new DashboardKpisDto {
                    SprintProgressValue = $"{completedTasks} / {totalTasks}",
                    SprintProgressSubtext = $"{progressPercent}% done",
                    SprintVelocityValue = (int)Math.Round(remainingHours),
                    SprintVelocitySubtext = $"{workingDaysLeft} working days left",
                    SprintHealthValue = healthScore,
                    SprintHealthSubtext = deliveryStatus,
                    TeamBurnoutRiskValue = capacityUsagePercent,
                    TeamBurnoutRiskSubtext = $"{overloadedCount} overloaded members"
                },
                LiveActivity = new List<ActivityFeedItemDto>(),
                Members = memberDtos,
                NeedsAttention = needsAttention,
                Risks = risks,
                Charts = new TeamPulseChartsDto
                {
                    Workload = workloadDistribution,
                },
            };

            return Result<TeamPulseDto>.Success(teamPulse);
        }

        public async Task<Result<List<ActivityFeedItemDto>>> GetRecentActivityAsync(Guid sprintId, CancellationToken ct = default)
        {
            var sprint = await _context.Set<Sprint>()
                .AsNoTracking()
                .Include(s => s.Project)
                .ThenInclude(p => p.ProjectEmployees)
                .ThenInclude(pe => pe.Employee)
                .FirstOrDefaultAsync(s => s.Id == sprintId, ct);

            if (sprint == null)
                return Result<List<ActivityFeedItemDto>>.Failure(SprintRiskErrors.SprintNotFound);

            var employeesById = sprint.Project.ProjectEmployees
                .Where(pe => pe.Employee != null)
                .ToDictionary(pe => pe.EmployeeId, pe => pe.Employee);

            var recentTasks = await _context.Set<TaskItem>()
                .AsNoTracking()
                .Where(t =>
                    t.SprintId == sprintId &&
                    !t.IsDeleted &&
                    t.ModifiedAt.HasValue &&
                    (t.Status == TaskItemStatus.InProgress || t.Status == TaskItemStatus.Review))
                .OrderByDescending(t => t.ModifiedAt)
                .Take(10)
                .Select(t => new
                {
                    t.Id,
                    t.TitleEn,
                    t.Status,
                    t.EmployeeId,
                    ModifiedAt = t.ModifiedAt!.Value
                })
                .ToListAsync(ct);

            var activities = recentTasks.Select(t =>
            {
                employeesById.TryGetValue(t.EmployeeId ?? Guid.Empty, out var emp);
                return new ActivityFeedItemDto
                {
                    Id = t.Id,
                    Initials = emp != null ? GetInitials(emp.FirstNameEn, emp.LastNameEn) : "TP",
                    Name = emp != null ? $"{emp.FirstNameEn} {emp.LastNameEn}" : "TaskPilot",
                    ActionType = GetTaskActivityType(t.Status),
                    Description = GetTaskActivityDescription(t.Status, t.TitleEn),
                    Timestamp = t.ModifiedAt,
                    TimeAgo = GetTimeAgo(t.ModifiedAt),
                    AgentTag = ""
                };
            }).ToList();

            return Result<List<ActivityFeedItemDto>>.Success(activities);
        }

        public async Task<Result<List<ActivityFeedItemDto>>> GetFullAuditLogAsync(Guid sprintId, CancellationToken ct = default)
        {
            var sprint = await _context.Set<Sprint>()
                .Include(s => s.Project)
                .ThenInclude(p => p.ProjectEmployees)
                .ThenInclude(pe => pe.Employee)
                .FirstOrDefaultAsync(s => s.Id == sprintId, ct);
                
            if (sprint == null)
                return Result<List<ActivityFeedItemDto>>.Failure(SprintRiskErrors.SprintNotFound);

            var alerts = await _context.Set<SprintRiskAlert>()
                .Include(a => a.AffectedEmployee)
                .Where(a => a.SprintId == sprintId)
                .ToListAsync(ct);

            var tasks = await _context.Set<TaskItem>()
                .Where(t => t.SprintId == sprintId && !t.IsDeleted)
                .ToListAsync(ct);

            var activities = new List<ActivityFeedItemDto>();
            
            foreach(var a in alerts)
            {
                activities.Add(new ActivityFeedItemDto {
                    Id = a.Id,
                    Initials = a.AffectedEmployee != null ? $"{a.AffectedEmployee.FirstNameEn.FirstOrDefault()}{a.AffectedEmployee.LastNameEn.FirstOrDefault()}" : "AI",
                    Name = a.AffectedEmployee != null ? $"{a.AffectedEmployee.FirstNameEn} {a.AffectedEmployee.LastNameEn}" : "System",
                    ActionType = a.Severity == RiskSeverity.Critical ? "CRITICAL" : "ALERT",
                    Description = a.MessageEn,
                    Timestamp = a.CreatedAt,
                    TimeAgo = GetTimeAgo(a.CreatedAt),
                    AgentTag = "Agile Coach"
                });
            }
            
            foreach(var t in tasks)
            {
                var emp = sprint.Project.ProjectEmployees.FirstOrDefault(pe => pe.EmployeeId == t.EmployeeId)?.Employee;
                
                // Add creation activity
                activities.Add(new ActivityFeedItemDto {
                    Id = Guid.NewGuid(), // synthetic id for activity
                    Initials = emp != null ? $"{emp.FirstNameEn.FirstOrDefault()}{emp.LastNameEn.FirstOrDefault()}" : "UK",
                    Name = emp != null ? $"{emp.FirstNameEn} {emp.LastNameEn}" : "Unknown",
                    ActionType = "INFO",
                    Description = $"Added task: {t.TitleEn}",
                    Timestamp = t.CreatedAt,
                    TimeAgo = GetTimeAgo(t.CreatedAt),
                    AgentTag = ""
                });

                // If done, add completion activity
                if (t.Status == TaskItemStatus.Done && t.ModifiedAt.HasValue)
                {
                    activities.Add(new ActivityFeedItemDto {
                        Id = t.Id,
                        Initials = emp != null ? $"{emp.FirstNameEn.FirstOrDefault()}{emp.LastNameEn.FirstOrDefault()}" : "UK",
                        Name = emp != null ? $"{emp.FirstNameEn} {emp.LastNameEn}" : "Unknown",
                        ActionType = "SUCCESS",
                        Description = $"Completed task: {t.TitleEn}",
                        Timestamp = t.ModifiedAt.Value,
                        TimeAgo = GetTimeAgo(t.ModifiedAt.Value),
                        AgentTag = ""
                    });
                }
            }

            var sortedActivities = activities.OrderByDescending(a => a.Timestamp).ToList();
            return Result<List<ActivityFeedItemDto>>.Success(sortedActivities);
        }

        private static int CountWorkingDays(DateTime startDate, DateTime endDate, int workingDaysMask)
        {
            if (endDate < startDate) return 0;

            var count = 0;
            for (var day = startDate.Date; day <= endDate.Date; day = day.AddDays(1))
            {
                if ((workingDaysMask & (1 << (int)day.DayOfWeek)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static decimal CalculateWorkingHours(DateTime start, DateTime end, int workingDaysMask, decimal hoursPerDay)
        {
            if (start >= end || hoursPerDay <= 0)
            {
                return 0;
            }

            const int startHour = 9;
            var firstDay = start.Date;
            var lastDay = end.Date;

            if (firstDay == lastDay)
            {
                return IsWorkingDay(firstDay.DayOfWeek, workingDaysMask)
                    ? CalculateSingleDayHours(start, end, startHour, hoursPerDay)
                    : 0;
            }

            decimal totalHours = 0;

            if (IsWorkingDay(firstDay.DayOfWeek, workingDaysMask))
            {
                totalHours += CalculateSingleDayHours(start, firstDay.AddHours(startHour + (double)hoursPerDay), startHour, hoursPerDay);
            }

            for (var day = firstDay.AddDays(1); day < lastDay; day = day.AddDays(1))
            {
                if (IsWorkingDay(day.DayOfWeek, workingDaysMask))
                {
                    totalHours += hoursPerDay;
                }
            }

            if (IsWorkingDay(lastDay.DayOfWeek, workingDaysMask))
            {
                totalHours += CalculateSingleDayHours(lastDay.AddHours(startHour), end, startHour, hoursPerDay);
            }

            return Math.Round(totalHours, 2);
        }

        private static bool IsWorkingDay(DayOfWeek day, int workingDaysMask)
        {
            return (workingDaysMask & (1 << (int)day)) != 0;
        }

        private static decimal CalculateSingleDayHours(DateTime dayStart, DateTime dayEnd, int startHour, decimal hoursPerDay)
        {
            var workStart = dayStart.Date.AddHours(startHour);
            var workEnd = dayStart.Date.AddHours(startHour + (double)hoursPerDay);
            var actualStart = dayStart > workStart ? dayStart : workStart;
            var actualEnd = dayEnd < workEnd ? dayEnd : workEnd;

            return actualStart < actualEnd ? (decimal)(actualEnd - actualStart).TotalHours : 0;
        }

        private static string GetLoadStatus(decimal assignedRemainingHours, decimal availableRemainingHours, int usagePercent)
        {
            if (assignedRemainingHours <= 0) return "Underused";
            if (availableRemainingHours <= 0) return "Overloaded";
            if (usagePercent > 110) return "Overloaded";
            if (usagePercent >= 90) return "NearLimit";
            if (usagePercent >= 70) return "Healthy";
            return "Available";
        }

        private static bool IsTaskStuck(TaskItem task, DateTime now, int workingDaysMask, decimal workingHoursPerDay)
        {
            if (task.Status != TaskItemStatus.InProgress)
            {
                return false;
            }

            return GetTaskInProgressWorkingDays(task, now, workingDaysMask) >= GetTaskStuckThresholdWorkingDays(task, workingHoursPerDay);
        }

        private static int GetTaskInProgressWorkingDays(TaskItem task, DateTime now, int workingDaysMask)
        {
            var startedAt = task.InProgressAt ?? task.ModifiedAt;
            if (!startedAt.HasValue)
            {
                return 0;
            }

            return CountWorkingDays(startedAt.Value.Date, now.Date, workingDaysMask);
        }

        private static int GetTaskStuckThresholdWorkingDays(TaskItem task, decimal workingHoursPerDay)
        {
            var dailyHours = workingHoursPerDay > 0 ? workingHoursPerDay : 8m;
            var expectedWorkingDays = task.EstimatedHours <= 0
                ? 1
                : (int)Math.Ceiling(task.EstimatedHours / dailyHours);

            return Math.Max(2, expectedWorkingDays + 1);
        }

        private static int CalculateWorkloadPressure(
            int usagePercent,
            int stuckTasksCount,
            int estimateExceededCount,
            int highPriorityTasksCount,
            int reviewTasksCount)
        {
            var pressure = Math.Clamp(usagePercent, 0, 120);
            pressure += Math.Min(20, stuckTasksCount * 10);
            pressure += Math.Min(15, estimateExceededCount * 8);
            pressure += Math.Min(10, highPriorityTasksCount * 3);
            pressure += Math.Min(10, reviewTasksCount * 3);
            return Math.Clamp(pressure, 0, 100);
        }

        private static string GetDeliveryStatus(
            int scheduleGap,
            int capacityUsagePercent,
            int overloadedCount,
            int stuckTasksCount,
            int workingDaysLeft,
            decimal remainingHours)
        {
            if (remainingHours <= 0) return "On Track";

            var critical = scheduleGap >= 30 ||
                           capacityUsagePercent > 120 ||
                           (workingDaysLeft <= 1 && remainingHours > 0) ||
                           stuckTasksCount >= 3;

            if (critical) return "Critical";

            var atRisk = scheduleGap >= 15 ||
                         capacityUsagePercent > 100 ||
                         overloadedCount > 0 ||
                         stuckTasksCount > 0;

            return atRisk ? "At Risk" : "On Track";
        }

        private static int CalculateHealthScore(
            int scheduleGap,
            int capacityUsagePercent,
            int overloadedCount,
            int stuckTasksCount,
            int estimateExceededCount)
        {
            var score = 100;
            if (scheduleGap > 0) score -= Math.Min(30, scheduleGap);
            if (capacityUsagePercent > 100) score -= Math.Min(25, capacityUsagePercent - 100);
            score -= Math.Min(20, overloadedCount * 8);
            score -= Math.Min(15, stuckTasksCount * 5);
            score -= Math.Min(10, estimateExceededCount * 3);
            return Math.Clamp(score, 0, 100);
        }

        private static List<NeedsAttentionItemDto> BuildNeedsAttention(
            List<TeamPulseMemberDto> members,
            List<TaskItem> unfinishedTasks,
            int scheduleGap,
            int capacityUsagePercent,
            int workingDaysLeft,
            decimal remainingHours,
            int reviewTasksCount,
            decimal reviewEstimatedHours,
            Func<TaskItem, decimal> getEffectiveActualHours,
            DateTime now,
            int workingDaysMask,
            decimal workingHoursPerDay)
        {
            var items = new List<NeedsAttentionItemDto>();

            foreach (var member in members.Where(m => m.UsagePercent > 110).Take(3))
            {
                items.Add(new NeedsAttentionItemDto
                {
                    Type = "Capacity",
                    Severity = "High",
                    Title = $"{member.Name} is overloaded",
                    Description = $"{FormatHours(member.AssignedRemainingHours)} assigned / {FormatHours(member.AvailableRemainingHours)} available",
                    ActionLabel = "Reassign tasks",
                    EmployeeId = member.EmployeeId
                });
            }

            foreach (var task in unfinishedTasks
                .Where(t => IsTaskStuck(t, now, workingDaysMask, workingHoursPerDay))
                .OrderByDescending(t => GetTaskInProgressWorkingDays(t, now, workingDaysMask) - GetTaskStuckThresholdWorkingDays(t, workingHoursPerDay))
                .Take(3))
            {
                var elapsedWorkingDays = GetTaskInProgressWorkingDays(task, now, workingDaysMask);
                var thresholdWorkingDays = GetTaskStuckThresholdWorkingDays(task, workingHoursPerDay);

                items.Add(new NeedsAttentionItemDto
                {
                    Type = "Execution",
                    Severity = "Medium",
                    Title = $"{task.TitleEn} may be stuck",
                    Description = $"In progress for {elapsedWorkingDays} working day(s); expected threshold is {thresholdWorkingDays}",
                    ActionLabel = "Review task",
                    TaskId = task.Id,
                    EmployeeId = task.EmployeeId
                });
            }

            foreach (var task in unfinishedTasks
                .Where(t => t.EstimatedHours > 0 && getEffectiveActualHours(t) > t.EstimatedHours)
                .OrderByDescending(t => getEffectiveActualHours(t) - t.EstimatedHours)
                .Take(3))
            {
                items.Add(new NeedsAttentionItemDto
                {
                    Type = "Execution",
                    Severity = "Medium",
                    Title = $"{task.TitleEn} exceeded estimate",
                    Description = $"{FormatHours(getEffectiveActualHours(task))} actual / {FormatHours(task.EstimatedHours)} estimated",
                    ActionLabel = "Review estimate",
                    TaskId = task.Id,
                    EmployeeId = task.EmployeeId
                });
            }

            if (scheduleGap >= 15)
            {
                items.Add(new NeedsAttentionItemDto
                {
                    Type = "Schedule",
                    Severity = scheduleGap >= 30 ? "Critical" : "High",
                    Title = "Sprint is behind schedule",
                    Description = $"Time used is {scheduleGap}% ahead of completed work",
                    ActionLabel = "Review scope"
                });
            }

            if (reviewTasksCount >= 3 || (remainingHours > 0 && reviewEstimatedHours / remainingHours >= 0.25m))
            {
                items.Add(new NeedsAttentionItemDto
                {
                    Type = "Flow",
                    Severity = reviewTasksCount >= 5 ? "High" : "Medium",
                    Title = "Review may become a bottleneck",
                    Description = $"{reviewTasksCount} task(s) in review, carrying {FormatHours(reviewEstimatedHours)} estimated work",
                    ActionLabel = "Clear review"
                });
            }

            if (capacityUsagePercent > 100)
            {
                items.Add(new NeedsAttentionItemDto
                {
                    Type = "Capacity",
                    Severity = capacityUsagePercent > 120 ? "Critical" : "High",
                    Title = "Remaining work exceeds team capacity",
                    Description = $"{FormatHours(remainingHours)} remaining across {workingDaysLeft} working days",
                    ActionLabel = "Rebalance workload"
                });
            }

            var helper = members
                .Where(m => m.UsagePercent < 50 && m.RemainingCapacityDeltaHours > 0)
                .OrderByDescending(m => m.RemainingCapacityDeltaHours)
                .FirstOrDefault();

            if (helper != null && members.Any(m => m.UsagePercent >= 90))
            {
                items.Add(new NeedsAttentionItemDto
                {
                    Type = "Capacity",
                    Severity = "Low",
                    Title = $"{helper.Name} can help",
                    Description = $"{FormatHours(helper.RemainingCapacityDeltaHours)} free capacity remaining",
                    ActionLabel = "Consider rebalance",
                    EmployeeId = helper.EmployeeId
                });
            }

            return items
                .OrderBy(i => i.Severity == "Critical" ? 0 : i.Severity == "High" ? 1 : 2)
                .ThenBy(i => i.Type)
                .ToList();
        }

        private static List<SprintHealthRiskDto> BuildRiskSummary(
            int overloadedCount,
            int scheduleGap,
            int capacityUsagePercent,
            int stuckTasksCount,
            int estimateExceededCount,
            int reviewTasksCount,
            decimal reviewEstimatedHours,
            decimal remainingHours)
        {
            var risks = new List<SprintHealthRiskDto>();

            risks.Add(new SprintHealthRiskDto
            {
                Type = "Capacity",
                Severity = capacityUsagePercent > 120 || overloadedCount > 0 ? "High" : "Low",
                Label = "Capacity risk",
                Description = overloadedCount > 0
                    ? $"{overloadedCount} member(s) are overloaded"
                    : $"{capacityUsagePercent}% of remaining team capacity is used",
                Count = overloadedCount
            });

            risks.Add(new SprintHealthRiskDto
            {
                Type = "Schedule",
                Severity = scheduleGap >= 30 ? "Critical" : scheduleGap >= 15 ? "High" : scheduleGap > 0 ? "Medium" : "Low",
                Label = "Schedule risk",
                Description = scheduleGap > 0
                    ? $"Time used is {scheduleGap}% ahead of completed work"
                    : "Progress is aligned with elapsed sprint time",
                Count = Math.Max(0, scheduleGap)
            });

            risks.Add(new SprintHealthRiskDto
            {
                Type = "Execution",
                Severity = stuckTasksCount >= 3 ? "High" : stuckTasksCount > 0 || estimateExceededCount > 0 ? "Medium" : "Low",
                Label = "Execution risk",
                Description = $"{stuckTasksCount} stuck task(s), {estimateExceededCount} estimate exceeded",
                Count = stuckTasksCount + estimateExceededCount
            });

            var reviewShare = remainingHours <= 0 ? 0 : (int)Math.Round((reviewEstimatedHours * 100m) / remainingHours);
            risks.Add(new SprintHealthRiskDto
            {
                Type = "Flow",
                Severity = reviewTasksCount >= 5 || reviewShare >= 35 ? "High" : reviewTasksCount >= 3 || reviewShare >= 25 ? "Medium" : "Low",
                Label = "Flow risk",
                Description = reviewTasksCount > 0
                    ? $"{reviewTasksCount} task(s) in review, {reviewShare}% of remaining work"
                    : "No review bottleneck detected",
                Count = reviewTasksCount
            });

            return risks;
        }

        private static string FormatHours(decimal hours)
        {
            return $"{Math.Round(hours, 1):0.#}h";
        }

        private static string GetTaskActivityType(TaskItemStatus status)
        {
            return status switch
            {
                TaskItemStatus.Done => "SUCCESS",
                TaskItemStatus.Review => "WARNING",
                TaskItemStatus.InProgress => "INFO",
                _ => "INFO"
            };
        }

        private static string GetTaskActivityDescription(TaskItemStatus status, string title)
        {
            return status switch
            {
                TaskItemStatus.Review => $"Moved to review: {title}",
                TaskItemStatus.InProgress => $"Started progress: {title}",
                _ => $"Updated task: {title}"
            };
        }

        private static string GetInitials(string? firstName, string? lastName)
        {
            var first = string.IsNullOrWhiteSpace(firstName) ? 'U' : firstName.Trim()[0];
            var last = string.IsNullOrWhiteSpace(lastName) ? 'K' : lastName.Trim()[0];
            return $"{first}{last}".ToUpperInvariant();
        }

        private string GetTimeAgo(DateTime timestamp)
        {
            var span = DateTime.UtcNow - timestamp;
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}
