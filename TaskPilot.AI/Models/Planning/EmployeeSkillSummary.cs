namespace TaskPilot.AI.Models.Planning
{
    public class EmployeeSkillSummary
    {
        public string SkillName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public decimal AvailableFte { get; set; }
        public int BeginnerCount { get; set; }
        public int IntermediateCount { get; set; }
        public int AdvancedCount { get; set; }
        public int ExpertCount { get; set; }

        /// <summary>
        /// Highest level available across all employees with this skill.
        /// Values match SkillLevel enum: Beginner, Intermediate, Advanced, Expert
        /// </summary>
        public string MaxLevel { get; set; } = string.Empty;
    }
}
