namespace TaskPilot.DTOs.Company
{
    public class EmployeeStatisticsDto
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int DeactivatedEmployees { get; set; }
        public int AvailableEmployees { get; set; }
        public int EmployeesInProjects { get; set; }
    }
}
