namespace TaskPilot.DTOs.Skills;

public class SkillMigrationReportDto
{
    public int CanonicalSkillId { get; set; }
    public string CanonicalSkillName { get; set; } = string.Empty;
    public int ObsoleteSkillsProcessed { get; set; }
    public int AliasesCreated { get; set; }
    public int EmployeeSkillsMigrated { get; set; }
    public int TaskRequiredSkillsMigrated { get; set; }
}
