namespace TaskPilot.DTOs.Assignment;

public class CapacityWarningDto
{
    public string Code { get; set; } = string.Empty;

    public double? ActualValue { get; set; }

    public double? LimitValue { get; set; }
}
