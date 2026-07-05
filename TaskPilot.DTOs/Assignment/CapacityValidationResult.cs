using System.Collections.Generic;

namespace TaskPilot.DTOs.Assignment;

public class CapacityValidationResult
{
    public bool CanProceed { get; set; }

    public double TeamCapacityHours { get; set; }

    public double RequiredHours { get; set; }

    public double CapacityUtilizationPercentage { get; set; }

    public int BlockersCount { get; set; }

    public int WarningsCount { get; set; }

    public long ValidationDurationMs { get; set; }

    public System.DateTime ValidationTimestampUtc { get; set; }

    public string ValidationVersion { get; set; } = string.Empty;

    public List<CapacityBlockerDto> Blockers { get; set; } = new();

    public List<CapacityWarningDto> Warnings { get; set; } = new();
}
