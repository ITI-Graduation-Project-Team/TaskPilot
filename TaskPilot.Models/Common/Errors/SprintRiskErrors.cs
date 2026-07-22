using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Models.Common.Errors
{
    public static class SprintRiskErrors
    {
        public static readonly Error AlertNotFound =
            new("SPRINT_RISK_ALERT_NOT_FOUND", ErrorType.NotFound);
        
        public static readonly Error SprintNotActive =
            new("SPRINT_NOT_ACTIVE", ErrorType.Validation);
        
        public static readonly Error SimulationFailed =
            new("RISK_SIMULATION_FAILED", ErrorType.Failure);
            
        public static readonly Error SprintNotFound =
            new("SPRINT_NOT_FOUND", ErrorType.NotFound);
    }
}
