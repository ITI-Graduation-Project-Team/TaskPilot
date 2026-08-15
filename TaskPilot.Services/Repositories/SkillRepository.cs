using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Data.Context;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Repositories
{
    public class SkillRepository : ISkillRepository
    {
        private readonly ApplicationDbContext _context;

        public SkillRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<EmployeeSkillSummary>> GetCompanySkillSummaryAsync(
            Guid companyId,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserSkills
                .Include(us => us.Skill)
                .Include(us => us.User)
                .Where(us =>
                    us.User is Employee &&
                    ((Employee)us.User).CompanyId == companyId)
                .GroupBy(us => us.Skill.Name)
                .Select(g => new EmployeeSkillSummary
                {
                    SkillName = g.Key,
                    EmployeeCount = g.Select(x => x.UserId).Distinct().Count(),
                    MaxLevel = g.Max(x => x.Level).ToString()
                })
                .ToListAsync(cancellationToken);
        }
        public async Task<List<EmployeeSkillSummary>> GetProjectSkillSummaryAsync(
             Guid projectId,
             CancellationToken cancellationToken = default)
        {
            var rows = await _context.ProjectEmployees
                .Where(pe => pe.ProjectId == projectId && pe.IsActive && !pe.Employee.IsDeactivated)
                .SelectMany(pe => pe.Employee.UserSkills.Select(us => new
                {
                    pe.EmployeeId,
                    pe.AllocationPercentage,
                    SkillName = us.Skill.Name,
                    us.Level
                }))
                .ToListAsync(cancellationToken);

            return rows
                .GroupBy(row => row.SkillName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new EmployeeSkillSummary
                {
                    SkillName = group.Key,
                    EmployeeCount = group.Select(row => row.EmployeeId).Distinct().Count(),
                    AvailableFte = group
                        .GroupBy(row => row.EmployeeId)
                        .Sum(employee => employee.First().AllocationPercentage) / 100m,
                    BeginnerCount = group.Count(row => row.Level == TaskPilot.Models.Enums.SkillLevel.Beginner),
                    IntermediateCount = group.Count(row => row.Level == TaskPilot.Models.Enums.SkillLevel.Intermediate),
                    AdvancedCount = group.Count(row => row.Level == TaskPilot.Models.Enums.SkillLevel.Advanced),
                    ExpertCount = group.Count(row => row.Level == TaskPilot.Models.Enums.SkillLevel.Expert),
                    MaxLevel = group.Max(row => row.Level).ToString()
                })
                .OrderBy(summary => summary.SkillName)
                .ToList();
        }
    }
}
