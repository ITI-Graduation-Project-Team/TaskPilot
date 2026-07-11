using System;

namespace TaskPilot.Services.DTOs
{
    public class WbsPersistenceResult
    {
        public Guid ProjectId { get; set; }
        public int UserStoriesCreated { get; set; }
        public int TasksCreated { get; set; }
    }
}
