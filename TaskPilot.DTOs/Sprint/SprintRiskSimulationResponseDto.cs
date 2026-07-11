using System;
using System.Collections.Generic;

namespace TaskPilot.DTOs.Sprint
{
    public class SprintRiskSimulationResponseDto
    {
        public Guid AlertId { get; set; }
        public List<WhatIfScenarioDto> Scenarios { get; set; } = new();
    }
}
