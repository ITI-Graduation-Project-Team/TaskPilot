using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Sprint;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace TaskPilot.Services.Implementations
{
    public class SprintDataCollectionService
    {
        private readonly IRepository<Sprint> _sprintRepository;
        private readonly IRepository<TaskItem> _taskRepository;
        private readonly IRepository<SprintRiskAlert> _riskAlertRepository;

        public SprintDataCollectionService(
            IRepository<Sprint> sprintRepository,
            IRepository<TaskItem> taskRepository,
            IRepository<SprintRiskAlert> riskAlertRepository)
        {
            _sprintRepository    = sprintRepository;
            _taskRepository      = taskRepository;
            _riskAlertRepository = riskAlertRepository;
        }

        public async Task<SprintRetrospectiveData> CollectAsync(
            Guid sprintId,
            CancellationToken cancellationToken = default)
        {
            var sprint = await _sprintRepository.GetByIdAsync(sprintId);

            if (sprint?.Status != SprintStatus.Completed)
                throw new InvalidOperationException(
                    "Retrospective is only available for completed sprints.");

            // ── Completed tasks ──────────────────────────────────────────────
            // These tasks still have TaskItem.SprintId set because sprint completion
            // only clears SprintId on *unfinished* tasks.
            // ── Completed tasks ──────────────────────────────────────────────
            // These tasks still have TaskItem.SprintId set because sprint completion
            // only clears SprintId on *unfinished* tasks.
            var doneTasks = await _taskRepository.GetQueryable()
                .Include(t => t.Employee)
                .Include(t => t.UserStory)
                .Where(t => t.SprintId == sprintId && t.Status == TaskItemStatus.Done)
                .ToListAsync(cancellationToken);

            // ── Unfinished tasks (via SprintRiskAlert) ───────────────────────
            // When a sprint completes, unfinished tasks have their SprintId and
            // EmployeeId set to NULL. The ONLY remaining link is the SprintRiskAlert,
            // which preserves both AffectedTaskId and AffectedEmployeeId.
            var unfinishedAlerts = await _riskAlertRepository.GetQueryable()
                .Include(a => a.AffectedTask).ThenInclude(t => t!.UserStory)
                .Include(a => a.AffectedEmployee)   // ← employee saved before clearing
                .Where(a => a.SprintId == sprintId
                         && a.RiskType == SprintRiskType.UnfinishedTask
                         && a.AffectedTaskId.HasValue)
                .ToListAsync(cancellationToken);

            // De-duplicate alerts per task (risk detection may have run multiple times)
            var uniqueUnfinishedAlerts = unfinishedAlerts
                .Where(a => a.AffectedTask != null)
                .GroupBy(a => a.AffectedTaskId!.Value)
                .Select(g => g.First())
                .ToList();

            var unfinishedTasks = uniqueUnfinishedAlerts
                .Select(a => a.AffectedTask!)
                .ToList();

            // ── Feature Completeness Index (Partially Completed Stories) ──────
            // Group all sprint tasks (Done + Unfinished) by UserStoryId
            var allTasks = doneTasks.Cast<TaskItem>().Concat(unfinishedTasks).ToList();
            var partiallyCompletedStories = allTasks
                .Where(t => t.UserStoryId.HasValue)
                .GroupBy(t => t.UserStoryId!.Value)
                .Select(g =>
                {
                    var story = g.First().UserStory;
                    var doneCountInStory = g.Count(t => t.Status == TaskItemStatus.Done);
                    var totalCountInStory = g.Count();
                    var pct = totalCountInStory > 0 ? (double)doneCountInStory / totalCountInStory * 100 : 0;

                    return new PartiallyCompletedStoryData
                    {
                        UserStoryId          = g.Key,
                        TitleEn              = story?.TitleEn ?? g.First().TitleEn,
                        TitleAr              = story?.TitleAr ?? g.First().TitleAr,
                        TotalTasks           = totalCountInStory,
                        CompletedTasks       = doneCountInStory,
                        RemainingTasks       = totalCountInStory - doneCountInStory,
                        CompletionPercentage = Math.Round(pct, 1)
                    };
                })
                .Where(s => s.CompletionPercentage > 0 && s.CompletionPercentage < 100)
                .OrderByDescending(s => s.CompletionPercentage)
                .ToList();

            // ── Aggregate totals ─────────────────────────────────────────────
            var totalCount     = doneTasks.Count + unfinishedTasks.Count;
            var totalEstimated = doneTasks.Sum(t => t.EstimatedHours)
                               + unfinishedTasks.Sum(t => t.EstimatedHours);
            var totalActual    = doneTasks.Sum(t => t.ActualHours);

            // ── Per-developer breakdown ──────────────────────────────────────
            // Collect all employee IDs from BOTH sources:
            //   doneTasks         → employee info still on TaskItem
            //   uniqueUnfinishedAlerts → employee info saved on SprintRiskAlert
            var doneByEmployee = doneTasks
                .Where(t => t.EmployeeId.HasValue)
                .GroupBy(t => t.EmployeeId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var unfinishedByEmployee = uniqueUnfinishedAlerts
                .Where(a => a.AffectedEmployeeId.HasValue)
                .GroupBy(a => a.AffectedEmployeeId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var allEmployeeIds = doneByEmployee.Keys
                .Union(unfinishedByEmployee.Keys)
                .Distinct()
                .ToList();

            var developerBreakdowns = allEmployeeIds.Select(empId =>
            {
                var devDone     = doneByEmployee.TryGetValue(empId, out var d) ? d : new List<TaskItem>();
                var devAlerts   = unfinishedByEmployee.TryGetValue(empId, out var u) ? u : new List<SprintRiskAlert>();
                var devUnfinished = devAlerts.Select(a => a.AffectedTask!).ToList();

                var totalAssigned  = devDone.Count + devUnfinished.Count;
                var devEstimated   = devDone.Sum(t => t.EstimatedHours)
                                   + devUnfinished.Sum(t => t.EstimatedHours);
                var devActual      = devDone.Sum(t => t.ActualHours);

                // Resolve employee name: prefer from done task, fallback to alert
                var employee = devDone.FirstOrDefault()?.Employee
                            ?? devAlerts.FirstOrDefault()?.AffectedEmployee;

                return new DeveloperRetrospectiveData
                {
                    EmployeeId         = empId,
                    FullName           = employee != null
                        ? $"{employee.FirstNameEn} {employee.LastNameEn}"
                        : "Unknown",
                    AssignedTasks      = totalAssigned,
                    CompletedTasks     = devDone.Count,
                    EstimatedHours     = devEstimated,
                    ActualHours        = devActual,
                    VelocityRatio      = devEstimated > 0
                        ? (double)(devActual / devEstimated)
                        : 0,
                    CompletionRate     = totalAssigned > 0
                        ? Math.Round((double)devDone.Count / totalAssigned * 100, 2)
                        : 0,
                    CompletedTaskTypes = devDone
                        .Select(t => t.Type.ToString())
                        .Distinct()
                        .ToList()
                };
            }).ToList();

            // ── Unfinished task details ──────────────────────────────────────
            var unfinishedTaskData = uniqueUnfinishedAlerts
                .Select(a =>
                {
                    var task     = a.AffectedTask!;
                    var assignee = a.AffectedEmployee;   // use alert employee (task.EmployeeId cleared)
                    return new UnfinishedTaskData
                    {
                        TaskId         = task.Id,
                        UserStoryId    = task.UserStoryId,
                        TitleEn        = task.TitleEn,
                        EstimatedHours = task.EstimatedHours,
                        Reason         = "NotStarted",
                        AssigneeName   = assignee != null
                            ? $"{assignee.FirstNameEn} {assignee.LastNameEn}"
                            : "Unassigned"
                    };
                })
                .ToList();

            return new SprintRetrospectiveData
            {
                SprintId                  = sprintId,
                SprintTitleEn             = sprint.TitleEn,
                StartDate                 = sprint.StartDate,
                EndDate                   = sprint.EndDate,
                ActualDurationDays        = (int)(sprint.EndDate - sprint.StartDate).TotalDays,
                PlannedDurationDays       = 14,
                TotalTasks                = totalCount,
                CompletedTasks            = doneTasks.Count,
                InProgressTasks           = 0,
                NotStartedTasks           = unfinishedTasks.Count,
                CompletionRate            = totalCount > 0
                    ? Math.Round((double)doneTasks.Count / totalCount * 100, 2)
                    : 0,
                TotalEstimatedHours       = totalEstimated,
                TotalActualHours          = totalActual,
                VelocityRatio             = totalEstimated > 0
                    ? (double)(totalActual / totalEstimated)
                    : 0,
                DeveloperBreakdowns       = developerBreakdowns,
                UnfinishedTasks           = unfinishedTaskData,
                PartiallyCompletedStories = partiallyCompletedStories
            };
        }
    }
}
