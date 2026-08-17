using TaskPilot.Models.Entities;

namespace TaskPilot.Services.Assignment;

internal static class AssignmentCapacityCalculator
{
    public static double CalculateMaxSprintHours(Sprint sprint, Company company, decimal allocationPercentage)
    {
        var workingDays = CalculateWorkingDays(sprint.StartDate, sprint.EndDate, company.WorkingDaysMask);
        return (double)(company.WorkingHoursPerDay
            * workingDays
            * (allocationPercentage / 100m)
            * company.DefaultCapacityBufferPercentage);
    }

    private static int CalculateWorkingDays(DateTime start, DateTime end, int workingDaysMask)
    {
        if (start > end) return 0;

        var workingDays = 0;
        for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
        {
            if ((workingDaysMask & (1 << (int)date.DayOfWeek)) != 0)
                workingDays++;
        }

        return workingDays;
    }
}
