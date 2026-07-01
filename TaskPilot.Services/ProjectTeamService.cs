using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services;

public class ProjectTeamService : IProjectTeamService
{
    private readonly IRepository<ProjectEmployee> _projectEmployeeRepository;
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<Employee> _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;

    public ProjectTeamService(
        IRepository<ProjectEmployee> projectEmployeeRepository,
        IRepository<Project> projectRepository,
        IRepository<Employee> employeeRepository,
        IUnitOfWork unitOfWork,
        ILocalizationService localizationService)
    {
        _projectEmployeeRepository = projectEmployeeRepository;
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
    }

    public async Task<Result> AssignEmployeesAsync(
        Guid projectId,
        AssignProjectEmployeesRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return Result.Failure(ProjectErrors.NotFound);

        if (request.Assignments == null || !request.Assignments.Any())
            return Result.Success();

        // Check for duplicates in request
        if (request.Assignments.GroupBy(x => x.EmployeeId).Any(g => g.Count() > 1))
            return Result.Failure(ProjectErrors.DuplicateAssignment);

        var employeeIds = request.Assignments.Select(a => a.EmployeeId).ToList();

        var employees = await _employeeRepository.GetQueryable()
            .Where(e => employeeIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        if (employees.Count != employeeIds.Count)
            return Result.Failure(ProjectErrors.EmployeeNotFound);

        if (employees.Any(e => e.CompanyId != project.CompanyId))
            return Result.Failure(ProjectErrors.InvalidCompany);

        var existingAssignments = await _projectEmployeeRepository.GetQueryable()
            .Where(pe => pe.ProjectId == projectId && employeeIds.Contains(pe.EmployeeId))
            .ToListAsync(cancellationToken);

        if (existingAssignments.Any())
            return Result.Failure(ProjectErrors.AlreadyAssigned);

        var newAssignments = request.Assignments.Select(a => new ProjectEmployee
        {
            ProjectId = projectId,
            EmployeeId = a.EmployeeId,
            Role = a.Role
        }).ToList();

        await _projectEmployeeRepository.AddRangeAsync(newAssignments);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<List<ProjectEmployeeDto>>> GetProjectEmployeesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var projectExists = await _projectRepository.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
            return Result<List<ProjectEmployeeDto>>.Failure(ProjectErrors.NotFound);

        var projectEmployees = await _projectEmployeeRepository.GetQueryable()
            .Include(pe => pe.Employee)
                .ThenInclude(e => e.UserSkills)
                    .ThenInclude(us => us.Skill)
            .Include(pe => pe.Employee)
                .ThenInclude(e => e.ProjectEmployees)
                    .ThenInclude(pe2 => pe2.Project)
            .Include(pe => pe.Employee)
                .ThenInclude(e => e.AssignedTasks)
                    .ThenInclude(t => t.Sprint)
            .Where(pe => pe.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var dtos = projectEmployees.Select(pe => {
            var activeProjectsCount = pe.Employee.ProjectEmployees.Count(x => x.Project != null && x.Project.Status != ProjectStatus.Completed);
            return new ProjectEmployeeDto
            {
                EmployeeId = pe.EmployeeId,
                FullName = $"{_localizationService.GetLocalizedProperty(pe.Employee.FirstNameEn, pe.Employee.FirstNameAr)} {_localizationService.GetLocalizedProperty(pe.Employee.LastNameEn, pe.Employee.LastNameAr)}".Trim(),
                Role = pe.Role,
                JobTitle = pe.Employee.JobTitle ?? string.Empty,
                SeniorityLevel = pe.Employee.SeniorityLevel ?? default,
                ActiveProjectsCount = activeProjectsCount,
                CurrentAssignedTasksCount = pe.Employee.AssignedTasks.Count(t => t.SprintId != null && t.Status != TaskItemStatus.Done && (t.Sprint == null || t.Sprint.Status == SprintStatus.Active)),
                CurrentSprintHours = (int)pe.Employee.AssignedTasks
                    .Where(t => t.Sprint != null && t.Sprint.ProjectId == projectId && t.Sprint.Status == SprintStatus.Active)
                    .Sum(t => t.EstimatedHours),
                AvailabilityStatus = EmployeeAvailabilityHelper.ComputeAvailabilityStatus(activeProjectsCount),
                Skills = pe.Employee.UserSkills.Select(us => us.Skill.Name).ToList()
            };
        }).ToList();

        return Result<List<ProjectEmployeeDto>>.Success(dtos);
    }

    public async Task<Result> RemoveEmployeeAsync(
        Guid projectId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _projectEmployeeRepository.GetQueryable()
            .Include(pe => pe.Employee)
                .ThenInclude(e => e.AssignedTasks)
                    .ThenInclude(t => t.Sprint)
            .FirstOrDefaultAsync(pe => pe.ProjectId == projectId && pe.EmployeeId == employeeId, cancellationToken);

        if (assignment == null)
            return Result.Failure(ProjectErrors.AssignmentNotFound);

        var hasActiveTasks = assignment.Employee.AssignedTasks.Any(t => 
            t.Sprint != null && 
            t.Sprint.ProjectId == projectId && 
            t.Sprint.Status == SprintStatus.Active);

        if (hasActiveTasks)
            return Result.Failure(ProjectErrors.EmployeeHasActiveTasks);

        _projectEmployeeRepository.Delete(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
