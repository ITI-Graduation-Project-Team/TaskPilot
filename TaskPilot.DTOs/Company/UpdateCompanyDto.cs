using Microsoft.AspNetCore.Http;

namespace TaskPilot.DTOs.Company
{
    public class UpdateCompanyDto
    {
        /// <summary>
        /// The new display name for the company.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optional logo image file to upload to Cloudinary.
        /// If not provided, an auto-generated avatar will be used when no logo exists.
        /// </summary>
        public IFormFile? Logo { get; set; }
    }
}
