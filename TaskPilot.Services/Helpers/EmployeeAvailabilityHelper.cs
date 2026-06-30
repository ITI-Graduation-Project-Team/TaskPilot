using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Helpers;

public static class EmployeeAvailabilityHelper
{
    public static string ComputeAvailabilityStatus(int activeProjectsCount)
        => activeProjectsCount switch
        {
            0 => "Available",
            1 or 2 => "PartiallyBusy",
            _ => "Busy"
        };
}
