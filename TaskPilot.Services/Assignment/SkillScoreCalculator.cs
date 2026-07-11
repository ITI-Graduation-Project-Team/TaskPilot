using TaskPilot.DTOs.Assignment;
using System.Linq;

namespace TaskPilot.Services.Assignment;

public class SkillScoreCalculator : IScoreCalculator
{
    public double Calculate(TaskSnapshotDto task, DeveloperSnapshotDto developer)
    {
        if (task.RequiredSkills == null || !task.RequiredSkills.Any())
            return 100;

        double totalScore = 0;
        
        foreach (var requiredSkill in task.RequiredSkills)
        {
            var developerSkill = developer.Skills?.FirstOrDefault(s => s.SkillName.Equals(requiredSkill.SkillName, System.StringComparison.OrdinalIgnoreCase));
            
            if (developerSkill != null)
            {
                totalScore += SkillLevelMatrix.GetScore(requiredSkill.RequiredLevel, developerSkill.Level);
            }
        }

        return totalScore / task.RequiredSkills.Count;
    }
}
