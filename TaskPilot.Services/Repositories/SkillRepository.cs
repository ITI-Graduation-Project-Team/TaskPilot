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
            return await _context.UserSkills
                .Include(us => us.Skill)
                .Include(us => us.User)
                .Where(us =>
                    us.User is Employee &&
                    _context.ProjectEmployees.Any(pe =>
                        pe.ProjectId == projectId &&
                        pe.EmployeeId == us.UserId))
                .GroupBy(us => us.Skill.Name)
                .Select(g => new EmployeeSkillSummary
                {
                    SkillName = g.Key,
                    EmployeeCount = g.Select(x => x.UserId).Distinct().Count(),
                    MaxLevel = g.Max(x => x.Level).ToString()
                })
                .ToListAsync(cancellationToken);
        }
    }
}
