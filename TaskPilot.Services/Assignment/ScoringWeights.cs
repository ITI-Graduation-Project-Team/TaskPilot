using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Services.Assignment;

public class ScoringWeights
{
    public int SkillWeight { get; set; } = 40;

    public int AvailabilityWeight { get; set; } = 30;

    public int VelocityWeight { get; set; } = 20;

    public int ExperienceWeight { get; set; } = 10;

    public Result Validate()
    {
        var total = SkillWeight + AvailabilityWeight + VelocityWeight + ExperienceWeight;

        if (total != 100)
        {
            return Result.Failure(AssignmentErrors.ScoringConfigurationInvalid);
        }

        return Result.Success();
    }
}
