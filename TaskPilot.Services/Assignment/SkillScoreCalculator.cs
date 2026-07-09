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
            var requiredNormalized = TaskPilot.Services.Helpers.SkillNormalizer.Normalize(requiredSkill.SkillName);
            var developerSkill = developer.Skills?.FirstOrDefault(s => 
                (s.SkillId > 0 && s.SkillId == requiredSkill.SkillId) || 
                TaskPilot.Services.Helpers.SkillNormalizer.Normalize(s.SkillName) == requiredNormalized ||
                requiredSkill.Aliases.Any(a => TaskPilot.Services.Helpers.SkillNormalizer.Normalize(a) == TaskPilot.Services.Helpers.SkillNormalizer.Normalize(s.SkillName))
            );
            
            if (developerSkill != null)
            {
                totalScore += SkillLevelMatrix.GetScore(requiredSkill.RequiredLevel, developerSkill.Level);
            }
        }

        return totalScore / task.RequiredSkills.Count;
    }
}
