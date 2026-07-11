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
        private readonly WhatIfSimulationAgent _simulationAgent;

        public SprintRiskService(
            ApplicationDbContext context,
            SprintRiskDetectionAgent detectionAgent,
            WhatIfSimulationAgent simulationAgent)
        {
            _context = context;
            _detectionAgent = detectionAgent;
            _simulationAgent = simulationAgent;
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
    }
}
