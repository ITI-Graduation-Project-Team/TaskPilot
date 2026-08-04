using Microsoft.AspNetCore.Http;
using TaskPilot.Models.Enums;

namespace TaskPilot.DTOs.Employees
{
    public class UpdateEmployeeProfileDto
    {
        public string FirstNameEn { get; set; } = string.Empty;
        public string LastNameEn { get; set; } = string.Empty;
        public string? FirstNameAr { get; set; }
        public string? LastNameAr { get; set; }
        public string? PhoneNumber { get; set; }
        public string? JobTitle { get; set; }
        public SeniorityLevel? SeniorityLevel { get; set; }
        public int? TotalYearsOfExperience { get; set; }
        
        public IFormFile? Avatar { get; set; }
        
        /// <summary>
        /// Only used if the updating user is an Employee, to update their CV.
        /// </summary>
        public IFormFile? CvFile { get; set; }
        
        public bool DeleteAvatar { get; set; }
    }
}
