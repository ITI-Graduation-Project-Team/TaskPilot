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
    private readonly IRepository<Sprint> _sprintRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public ProjectTeamService(
        IRepository<ProjectEmployee> projectEmployeeRepository,
        IRepository<Project> projectRepository,
        IRepository<Employee> employeeRepository,
        IRepository<Sprint> sprintRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService)
    {
        _projectEmployeeRepository = projectEmployeeRepository;
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
        _sprintRepository = sprintRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<Result<AssignEmployeesResultDto>> AssignEmployeesAsync(
        Guid projectId,
        AssignProjectEmployeesRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            return Result.Failure<AssignEmployeesResultDto>(new Error("ProjectNotFound", ErrorType.Validation, "Project not found."));

        if (request.Assignments == null || !request.Assignments.Any())
            return Result.Success(new AssignEmployeesResultDto());

        // Check for duplicates in request
        if (request.Assignments.GroupBy(x => x.EmployeeId).Any(g => g.Count() > 1))
            return Result.Failure<AssignEmployeesResultDto>(new Error("DuplicateAssignment", ErrorType.Validation, "Duplicate assignments are forbidden."));

        var employeeIds = request.Assignments.Select(a => a.EmployeeId).ToList();

        var employees = await _employeeRepository.GetQueryable()
            .Where(e => employeeIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        if (employees.Count != employeeIds.Count)
            return Result.Failure<AssignEmployeesResultDto>(new Error("EmployeeNotFound", ErrorType.Validation, "One or more employees not found."));

        if (employees.Any(e => e.IsDeactivated))
            return Result.Failure<AssignEmployeesResultDto>(new Error("EmployeeDeactivated", ErrorType.Validation, "Cannot assign deactivated employees to a project."));

        if (employees.Any(e => e.CompanyId != project.CompanyId))
            return Result.Failure<AssignEmployeesResultDto>(new Error("InvalidCompany", ErrorType.Validation, "Only Employees from the same Company may be assigned."));

        var existingAssignments = await _projectEmployeeRepository.GetQueryable()
            .Where(pe => pe.ProjectId == projectId && employeeIds.Contains(pe.EmployeeId))
            .ToListAsync(cancellationToken);

        if (existingAssignments.Any())
            return Result.Failure<AssignEmployeesResultDto>(new Error("AlreadyAssigned", ErrorType.Validation, "One or more employees are already assigned to the project."));

        var alreadyAssignedToActiveProject = await _projectEmployeeRepository.GetQueryable()
            .AnyAsync(pe => employeeIds.Contains(pe.EmployeeId) && 
                            pe.ProjectId != projectId && 
                            pe.Project.Status != ProjectStatus.Completed && 
                            pe.Project.Status != ProjectStatus.Archived, 
                      cancellationToken);

        if (alreadyAssignedToActiveProject)
            return Result.Failure<AssignEmployeesResultDto>(new Error("EmployeeAlreadyAssignedToAnotherProject", ErrorType.Validation, "One or more employees are already assigned to another active project."));

        var newAssignments = request.Assignments.Select(a => new ProjectEmployee
        {
            ProjectId = projectId,
            EmployeeId = a.EmployeeId,
            Role = a.Role,
            AllocationPercentage = a.AllocationPercentage
        }).ToList();

        await _projectEmployeeRepository.AddRangeAsync(newAssignments);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var employeeId in employeeIds)
        {
            await _notificationService.SendAsync(
                userId: employeeId,
                type: NotificationType.UserAddedToProject,
                messageEn: $"You have been added to project '{project.NameEn}'.",
                messageAr: $"تمت إضافتك إلى مشروع '{project.NameAr ?? project.NameEn}'.",
                url: $"/projects/{projectId}"
            );
        }

        var plannedSprints = await _sprintRepository.GetQueryable()
            .Where(s => s.ProjectId == projectId && s.Status == SprintStatus.Planned)
            .Select(s => new { s.Id, s.TitleEn })
            .ToListAsync(cancellationToken);

        return Result.Success(new AssignEmployeesResultDto
        {
            HasPlannedSprints = plannedSprints.Any(),
            PlannedSprintNames = plannedSprints.Select(s => s.TitleEn ?? "").ToList(),
            PlannedSprintIds = plannedSprints.Select(s => s.Id).ToList()
        });
    }

    public async Task<Result<List<ProjectEmployeeDto>>> GetProjectEmployeesAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var projectExists = await _projectRepository.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
            return Result<List<ProjectEmployeeDto>>.Failure(new Error("Project.NotFound", ErrorType.NotFound, "Project not found."));

        var dtos = await _projectEmployeeRepository.GetQueryable()
            .Where(pe => pe.ProjectId == projectId)
            .Select(pe => new ProjectEmployeeDto
            {
                EmployeeId = pe.EmployeeId,
                FullName = (pe.Employee.FirstNameEn + " " + pe.Employee.LastNameEn).Trim(),
                Role = pe.Role,
                AllocationPercentage = pe.AllocationPercentage,
                JobTitle = pe.Employee.JobTitle ?? string.Empty,
                SeniorityLevel = pe.Employee.SeniorityLevel ?? default,
                ActiveProjectsCount = pe.Employee.ProjectEmployees.Count(x => x.Project != null && x.Project.Status != TaskPilot.Models.Enums.ProjectStatus.Completed),
                CurrentAssignedTasksCount = pe.Employee.AssignedTasks.Count(t => t.SprintId != null && t.Status != TaskPilot.Models.Enums.TaskItemStatus.Done && (t.Sprint == null || t.Sprint.Status == TaskPilot.Models.Enums.SprintStatus.Active)),
                CurrentSprintHours = (int)pe.Employee.AssignedTasks
                    .Where(t => t.Sprint != null && t.Sprint.ProjectId == projectId && t.Sprint.Status == TaskPilot.Models.Enums.SprintStatus.Active)
                    .Sum(t => t.EstimatedHours),
                Skills = pe.Employee.UserSkills.Select(us => us.Skill.Name).ToList(),
                IsDeactivated = pe.Employee.IsDeactivated,
                DeactivationReason = pe.Employee.DeactivationReason,
                DeactivatedAt = pe.Employee.DeactivatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var dto in dtos)
        {
            dto.AvailabilityStatus = EmployeeAvailabilityHelper.ComputeAvailabilityStatus(dto.ActiveProjectsCount);
        }

        return Result<List<ProjectEmployeeDto>>.Success(dtos);
    }

    public async Task<Result<int>> GetProjectEmployeesCountAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var count = await _projectEmployeeRepository.GetQueryable()
            .CountAsync(pe => pe.ProjectId == projectId, cancellationToken);
            
        return Result<int>.Success(count);
    }

    public async Task<Result<AssignEmployeesResultDto>> RemoveEmployeeAsync(
        Guid projectId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _projectEmployeeRepository.GetQueryable()
            .Include(pe => pe.Employee)
                .ThenInclude(e => e.AssignedTasks)
                    .ThenInclude(t => t.Sprint)
            .Include(pe => pe.Employee)
                .ThenInclude(e => e.AssignedTasks)
                    .ThenInclude(t => t.UserStory)
            .Include(pe => pe.Project)
                .ThenInclude(p => p.Sprints)
            .FirstOrDefaultAsync(pe => pe.ProjectId == projectId && pe.EmployeeId == employeeId, cancellationToken);

        if (assignment == null)
            return Result<AssignEmployeesResultDto>.Failure(new Error("Assignment.NotFound", ErrorType.NotFound, "Employee is not assigned to this project."));

        var hasActiveTasks = assignment.Employee.AssignedTasks.Any(t => 
            t.Sprint != null && 
            t.Sprint.ProjectId == projectId && 
            t.Sprint.Status == SprintStatus.Active);

        if (hasActiveTasks)
            return Result<AssignEmployeesResultDto>.Failure(new Error("EmployeeHasActiveTasks", ErrorType.Validation, "Employee still owns active tasks in an ongoing sprint."));

        // Analyze planned sprints before removing
        var plannedSprints = assignment.Project?.Sprints?.Where(s => s.Status == SprintStatus.Planned).ToList() ?? new List<Sprint>();
        var affectedSprints = new List<Sprint>();

        foreach (var sprint in plannedSprints)
        {
            var employeeHasTasks = assignment.Employee.AssignedTasks.Any(t => t.SprintId == sprint.Id);
            if (employeeHasTasks || assignment.CreatedAt <= sprint.CreatedAt)
            {
                affectedSprints.Add(sprint);
            }
        }

        // Unassign the employee from any tasks in this project that are NOT in an active sprint
        // (e.g. Backlog tasks or tasks in Pending/Future sprints)
        var pendingTasks = assignment.Employee.AssignedTasks.Where(t =>
            (t.Sprint?.ProjectId == projectId || t.UserStory?.ProjectId == projectId) &&
            (t.Sprint == null || t.Sprint.Status != SprintStatus.Active) &&
            t.Status != TaskItemStatus.Done).ToList();

        foreach (var task in pendingTasks)
        {
            task.EmployeeId = null;
        }

        _projectEmployeeRepository.Delete(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AssignEmployeesResultDto>.Success(new AssignEmployeesResultDto
        {
            HasPlannedSprints = affectedSprints.Any(),
            PlannedSprintNames = affectedSprints.Select(s => s.TitleEn ?? "").ToList(),
            PlannedSprintIds = affectedSprints.Select(s => s.Id).ToList()
        });
    }
}
