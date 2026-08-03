namespace TaskPilot.DTOs.Company
{
    public class CompanyResponse
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public Guid OwnerId { get; set; }

        // Logo URL returned to the frontend after update
        public string? LogoUrl { get; set; }
    }
}
