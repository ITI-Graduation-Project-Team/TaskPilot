namespace TaskPilot.DTOs.Company
{
    public class EmployeeSuggestionDTO
    {
        public Guid Id { get; set; }

        public string FullName { get; set; }
            = string.Empty;
        public string Email { get; set; }
            = string.Empty;
        public bool HasCompany { get; set; }
    }
}
