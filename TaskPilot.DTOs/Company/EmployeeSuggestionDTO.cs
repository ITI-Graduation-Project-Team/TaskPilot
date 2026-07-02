namespace TaskPilot.DTOs.Company
{
    public class EmployeeSuggestionDTO
    {
        public Guid Id { get; set; }

        public string FullName { get; set; }
            = string.Empty;
        public string Email { get; set; }
            = string.Empty;
        public TaskPilot.Models.Enums.EmployeeSearchStatus Status { get; set; }
        public string? StatusMessage { get; set; }
    }
}
