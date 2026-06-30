namespace TaskPilot.AI.Models.Planning
{
    public class EmployeeSkillSummary
    {
        public string SkillName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }

        /// <summary>
        /// Highest level available across all employees with this skill.
        /// Values match SkillLevel enum: Beginner, Intermediate, Advanced, Expert
        /// </summary>
        public string MaxLevel { get; set; } = string.Empty;
    }
}
