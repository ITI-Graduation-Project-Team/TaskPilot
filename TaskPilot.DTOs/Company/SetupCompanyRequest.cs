namespace TaskPilot.DTOs.Company
{
    public class SetupCompanyRequest
    {
        public string CompanyName { get; set; }
            = string.Empty;


        public string? PolicyTitleEn { get; set; }

        public string? PolicyTitleAr { get; set; }

        public string? PolicyContentEn { get; set; }

        public string? PolicyContentAr { get; set; }

        public string? PolicyDocumentUrl { get; set; }


        public List<string> EmployeeEmails
        { get; set; } = new();
    }
}
