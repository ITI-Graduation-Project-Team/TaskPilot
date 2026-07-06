using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Assignment;

public static class SkillLevelMatrix
{
    private static readonly Dictionary<(SkillLevel Required, SkillLevel Actual), double> Matrix = new()
    {
        // Required: Beginner
        { (SkillLevel.Beginner, SkillLevel.Beginner), 100 },
        { (SkillLevel.Beginner, SkillLevel.Intermediate), 100 },
        { (SkillLevel.Beginner, SkillLevel.Advanced), 100 },
        { (SkillLevel.Beginner, SkillLevel.Expert), 100 },

        // Required: Intermediate
        { (SkillLevel.Intermediate, SkillLevel.Beginner), 50 },
        { (SkillLevel.Intermediate, SkillLevel.Intermediate), 100 },
        { (SkillLevel.Intermediate, SkillLevel.Advanced), 100 },
        { (SkillLevel.Intermediate, SkillLevel.Expert), 100 },

        // Required: Advanced
        { (SkillLevel.Advanced, SkillLevel.Beginner), 0 },
        { (SkillLevel.Advanced, SkillLevel.Intermediate), 50 },
        { (SkillLevel.Advanced, SkillLevel.Advanced), 100 },
        { (SkillLevel.Advanced, SkillLevel.Expert), 100 },

        // Required: Expert
        { (SkillLevel.Expert, SkillLevel.Beginner), 0 },
        { (SkillLevel.Expert, SkillLevel.Intermediate), 0 },
        { (SkillLevel.Expert, SkillLevel.Advanced), 50 },
        { (SkillLevel.Expert, SkillLevel.Expert), 100 }
    };

    public static double GetScore(SkillLevel required, SkillLevel actual)
    {
        if (Matrix.TryGetValue((required, actual), out var score))
        {
            return score;
        }

        return 0;
    }
}
