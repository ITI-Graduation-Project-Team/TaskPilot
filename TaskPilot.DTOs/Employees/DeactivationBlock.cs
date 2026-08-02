using System.Text.Json.Serialization;

namespace TaskPilot.DTOs.Employees;

public abstract class DeactivationBlock
{
    [JsonPropertyName("$type")]
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public class ActiveTasksBlock : DeactivationBlock
{
    public List<TaskRef> Tasks { get; set; } = new();
    
    public ActiveTasksBlock() 
    {
        Type = "ActiveTasks";
        Severity = "High";
    }
}

public class ProjectManagerBlock : DeactivationBlock
{
    public List<ProjectRef> ManagedProjects { get; set; } = new();
    
    public ProjectManagerBlock()
    {
        Type = "ProjectManager";
        Severity = "Critical";
    }
}

public class TaskRef
{
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ProjectRef
{
    public string Name { get; set; } = string.Empty;
}
