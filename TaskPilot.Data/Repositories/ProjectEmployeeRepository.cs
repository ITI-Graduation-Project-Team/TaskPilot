using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Context;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data.Repositories
{
    public class ProjectEmployeeRepository : IProjectEmployeeRepository
    {
        private readonly ApplicationDbContext _context;

        public ProjectEmployeeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HashSet<Guid>> GetEmployeeIdsByProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var ids = await _context.ProjectEmployees
                .Where(pe => pe.ProjectId == projectId)
                .Select(pe => pe.EmployeeId)
                .ToListAsync(cancellationToken);

            return ids.ToHashSet();
        }

        public async Task<bool> IsProjectManagerAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
        {
            return await _context.Projects
                .AsNoTracking()
                .AnyAsync(
                    p => p.Id == projectId &&
                         p.ManagerId == userId,
                    cancellationToken);
        }

        public async Task<List<ProjectEmployee>> GetActiveByEmployeeIdAsync(
            Guid employeeId,
            CancellationToken cancellationToken = default)
        {
            return await _context.ProjectEmployees
                .Where(pe => pe.EmployeeId == employeeId && pe.IsActive)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ProjectEmployee>> GetActiveByProjectIdAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return await _context.ProjectEmployees
                .Where(pe => pe.ProjectId == projectId && pe.IsActive)
                .ToListAsync(cancellationToken);
        }
    }
}
