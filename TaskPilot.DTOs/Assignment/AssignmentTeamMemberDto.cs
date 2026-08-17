namespace TaskPilot.DTOs.Assignment;

public class AssignmentTeamMemberDto
{
    public Guid EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
}
