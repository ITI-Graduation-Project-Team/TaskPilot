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
                .Include(s => s.Tasks)
                .Include(s => s.Project)
                .ThenInclude(p => p.ProjectEmployees)
                .ThenInclude(pe => pe.Employee)
                .FirstOrDefaultAsync(s => s.Id == sprintId, ct);
                
            if (sprint == null)
                return Result<TeamPulseDto>.Failure(SprintRiskErrors.SprintNotFound);

            // Force real-time AI burnout analysis before returning the pulse data
            await AnalyzeSprintBurnoutAsync(sprintId, ct);

            var latestSnapshots = await _context.Set<SprintBurnoutSnapshot>()
                .Include(s => s.Employee)
                .Where(s => s.SprintId == sprintId)
                .GroupBy(s => s.EmployeeId)
                .Select(g => g.OrderByDescending(s => s.AnalyzedAt).FirstOrDefault())
                .ToListAsync(ct);

            var historySnapshots = await _context.Set<SprintBurnoutSnapshot>()
                .Where(s => s.SprintId == sprintId && s.AnalyzedAt >= DateTime.UtcNow.AddDays(-7))
                .OrderBy(s => s.AnalyzedAt)
                .ToListAsync(ct);

            var activeAlertsCount = await _context.Set<SprintRiskAlert>()
                .CountAsync(a => a.SprintId == sprintId && !a.IsDismissed, ct);

            // KPI 1: Progress
            int totalTasks = sprint.Tasks.Count(t => !t.IsDeleted);
            int completedTasks = sprint.Tasks.Count(t => !t.IsDeleted && t.Status == TaskItemStatus.Done);
            int approachingDeadline = sprint.Tasks.Count(t => !t.IsDeleted && t.Status != TaskItemStatus.Done && (sprint.EndDate - DateTime.UtcNow).TotalDays < 2);
            
            // KPI 2: Velocity
            int totalVelocity = (int)sprint.Tasks.Where(t => !t.IsDeleted).Sum(t => t.ActualHours);
            int targetVelocity = (int)sprint.Project.ProjectEmployees.Sum(pe => pe.Employee.MaxSprintHours ?? 40);
            
            // KPI 3: Health
            int overdueTasksCount = sprint.Tasks.Count(t => !t.IsDeleted && t.Status != TaskItemStatus.Done && t.ActualHours > t.EstimatedHours);
            int healthScore = Math.Max(0, 100 - (activeAlertsCount * 10) - (overdueTasksCount * 5));
            
            // KPI 4: Burnout
            int teamBurnoutAvg = latestSnapshots.Any(s => s != null) ? (int)latestSnapshots.Where(s => s != null).Average(s => s!.BurnoutScore) : 0;
            int highRiskCount = latestSnapshots.Count(s => s != null && s.RiskLevel == "High");

            // Activity Feed (Alerts & Task Completions)
            var alerts = await _context.Set<SprintRiskAlert>()
                .Include(a => a.AffectedEmployee)
                .Where(a => a.SprintId == sprintId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToListAsync(ct);

            var doneTasks = sprint.Tasks
                .Where(t => t.Status == TaskItemStatus.Done && t.ModifiedAt != null)
                .OrderByDescending(t => t.ModifiedAt)
                .Take(5)
                .ToList();

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
            
            foreach(var t in doneTasks)
            {
                var emp = sprint.Project.ProjectEmployees.FirstOrDefault(pe => pe.EmployeeId == t.EmployeeId)?.Employee;
                activities.Add(new ActivityFeedItemDto {
                    Id = t.Id,
                    Initials = emp != null ? $"{emp.FirstNameEn.FirstOrDefault()}{emp.LastNameEn.FirstOrDefault()}" : "UK",
                    Name = emp != null ? $"{emp.FirstNameEn} {emp.LastNameEn}" : "Unknown",
                    ActionType = "SUCCESS",
                    Description = $"Completed task: {t.TitleEn}",
                    Timestamp = t.ModifiedAt ?? DateTime.UtcNow,
                    TimeAgo = GetTimeAgo(t.ModifiedAt ?? DateTime.UtcNow),
                    AgentTag = ""
                });
            }

            // --- CHARTS CALCULATIONS ---
            
            // 1. Top Contributors
            var topContributors = sprint.Tasks
                .Where(t => t.Status == TaskItemStatus.Done && !t.IsDeleted && t.EmployeeId.HasValue)
                .GroupBy(t => t.EmployeeId)
                .Select(g => new {
                    EmployeeId = g.Key,
                    CompletedHours = g.Sum(t => t.ActualHours),
                    CompletedTasksCount = g.Count()
                })
                .OrderByDescending(x => x.CompletedHours)
                .Take(3)
                .ToList();
                
            var topContributorDtos = topContributors.Select(tc => {
                var emp = sprint.Project.ProjectEmployees.FirstOrDefault(pe => pe.EmployeeId == tc.EmployeeId)?.Employee;
                return new TopContributorDto
                {
                    Initials = emp != null ? $"{emp.FirstNameEn.FirstOrDefault()}{emp.LastNameEn.FirstOrDefault()}" : "UK",
                    Name = emp != null ? $"{emp.FirstNameEn} {emp.LastNameEn}" : "Unknown",
                    CompletedHours = (int)tc.CompletedHours,
                    CompletedTasksCount = tc.CompletedTasksCount
                };
            }).ToList();

            // 2. Workload Distribution
            var activeTasksWorkload = sprint.Tasks
                .Where(t => t.Status != TaskItemStatus.Done && !t.IsDeleted && t.EmployeeId.HasValue)
                .Select(t => new { 
                    t.EstimatedHours, 
                    JobTitle = sprint.Project.ProjectEmployees.FirstOrDefault(pe => pe.EmployeeId == t.EmployeeId)?.Employee?.JobTitle ?? "Unspecified" 
                })
                .GroupBy(x => x.JobTitle)
                .Select(g => new { JobTitle = g.Key, TotalHours = g.Sum(x => x.EstimatedHours) })
                .ToList();

            var workloadDistribution = new WorkloadDistributionDto
            {
                Labels = activeTasksWorkload.Select(w => w.JobTitle).ToList(),
                Series = activeTasksWorkload.Select(w => (int)w.TotalHours).ToList()
            };

            // 3. Sprint Burndown (Simulated for visualization)
            int totalSprintEstimatedHours = (int)sprint.Tasks.Where(t => !t.IsDeleted).Sum(t => t.EstimatedHours);
            int sprintDurationDays = (sprint.EndDate - sprint.StartDate).Days > 0 ? (sprint.EndDate - sprint.StartDate).Days : 1;
            int daysPassed = (DateTime.UtcNow - sprint.StartDate).Days;
            daysPassed = Math.Clamp(daysPassed, 0, sprintDurationDays);
            
            var burndownLabels = new List<string>();
            var idealTrend = new List<int>();
            var actualTrend = new List<int>();
            
            int remainingHoursNow = (int)sprint.Tasks.Where(t => !t.IsDeleted && t.Status != TaskItemStatus.Done).Sum(t => t.EstimatedHours);
            
            for (int i = 0; i <= sprintDurationDays; i++)
            {
                DateTime currentDay = sprint.StartDate.AddDays(i);
                burndownLabels.Add(currentDay.ToString("ddd")); // e.g. Mon, Tue
                
                int idealRemaining = totalSprintEstimatedHours - (totalSprintEstimatedHours / sprintDurationDays * i);
                idealTrend.Add(Math.Max(0, idealRemaining));
                
                if (i <= daysPassed)
                {
                    if (i == daysPassed) actualTrend.Add(remainingHoursNow);
                    else if (i == 0) actualTrend.Add(totalSprintEstimatedHours);
                    else
                    {
                        int diff = totalSprintEstimatedHours - remainingHoursNow;
                        int simulatedBurn = totalSprintEstimatedHours - (diff / (daysPassed == 0 ? 1 : daysPassed) * i);
                        actualTrend.Add(simulatedBurn);
                    }
                }
            }
            
            var burndownDto = new SprintBurndownDto
            {
                Labels = burndownLabels,
                IdealTrend = idealTrend,
                ActualTrend = actualTrend
            };

            var teamPulse = new TeamPulseDto
            {
                TeamBurnoutRisk = teamBurnoutAvg,
                Kpis = new DashboardKpisDto {
                    SprintProgressValue = $"{completedTasks} / {totalTasks}",
                    SprintProgressSubtext = $"{approachingDeadline} approaching deadline",
                    SprintVelocityValue = totalVelocity,
                    SprintVelocitySubtext = $"Target: {targetVelocity} hrs/sprint",
                    SprintHealthValue = healthScore,
                    SprintHealthSubtext = healthScore < 70 ? "Below 70% threshold" : "On track",
                    TeamBurnoutRiskValue = teamBurnoutAvg,
                    TeamBurnoutRiskSubtext = $"{highRiskCount} members at HIGH risk"
                },
                LiveActivity = activities.OrderByDescending(a => a.Timestamp).Take(10).ToList(),
                Charts = new TeamPulseChartsDto
                {
                    TopContributors = topContributorDtos,
                    Workload = workloadDistribution,
                    Burndown = burndownDto
                },
                Members = sprint.Project.ProjectEmployees.Select(pe => {
                    var emp = pe.Employee;
                    var s = latestSnapshots.FirstOrDefault(snap => snap?.EmployeeId == emp.Id);
                    
                    if (s != null)
                    {
                        return new TeamPulseMemberDto
                        {
                            EmployeeId = emp.Id,
                            Initials = $"{emp.FirstNameEn.FirstOrDefault()}{emp.LastNameEn.FirstOrDefault()}",
                            Name = $"{emp.FirstNameEn} {emp.LastNameEn}",
                            JobTitle = emp.JobTitle ?? "Software Engineer",
                            RiskLevel = s.RiskLevel,
                            BurnoutScore = s.BurnoutScore,
                            RiskFactors = new RiskFactorsDto
                            {
                                Workload = s.WorkloadScore,
                                Pace = s.PaceScore,
                                Engagement = s.EngagementScore
                            },
                            TrendDirection = s.TrendDirection,
                            History = historySnapshots.Where(h => h.EmployeeId == emp.Id).Select(h => h.BurnoutScore).ToList()
                        };
                    }
                    else
                    {
                        // Fallback for members who haven't been analyzed yet
                        return new TeamPulseMemberDto
                        {
                            EmployeeId = emp.Id,
                            Initials = $"{emp.FirstNameEn.FirstOrDefault()}{emp.LastNameEn.FirstOrDefault()}",
                            Name = $"{emp.FirstNameEn} {emp.LastNameEn}",
                            JobTitle = emp.JobTitle ?? "Software Engineer",
                            RiskLevel = "Healthy",
                            BurnoutScore = 0,
                            RiskFactors = new RiskFactorsDto { Workload = 0, Pace = 0, Engagement = 0 },
                            TrendDirection = "Stable",
                            History = new List<int> { 0 }
                        };
                    }
                }).ToList()
            };

            return Result<TeamPulseDto>.Success(teamPulse);
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
