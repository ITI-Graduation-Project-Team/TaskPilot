using System;
using System.Collections.Generic;
using System.Text;

namespace TaskPilot.DTOs.Calender
{
    public class WorkloadBreakdownDto
    {
        public int AssignedMinutes { get; set; }

        public int MeetingMinutes { get; set; }

        public int PersonalMinutes { get; set; }

        public int BlockerMinutes { get; set; }

        public int TotalMinutes { get; set; }
    }
}
