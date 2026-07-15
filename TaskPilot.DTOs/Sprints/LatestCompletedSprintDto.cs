namespace TaskPilot.DTOs.Sprints
{
    public class LatestCompletedSprintDto
    {
        public Guid SprintId { get; set; }
        public string TitleEn { get; set; } = string.Empty;
        public string TitleAr { get; set; } = string.Empty;
        public DateTime EndDate { get; set; }
    }
}
