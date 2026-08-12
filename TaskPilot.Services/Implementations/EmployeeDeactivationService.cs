using TaskPilot.Services.Interfaces;
using TaskPilot.DTOs.Employees;
using TaskPilot.Models.Common.Results;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Models.Enums;
using TaskPilot.Models.Common.Errors;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace TaskPilot.Services.Implementations;

public class EmployeeDeactivationService : IEmployeeDeactivationService
{
    private readonly IRepository<Employee> _employeeRepository;
    private readonly IRepository<Project> _projectRepository;
    private readonly IRepository<TaskItem> _taskRepository;
    private readonly IRepository<EmployeeInvitation> _invitationRepository;
    private readonly IRepository<ProjectEmployee> _projectEmployeeRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public EmployeeDeactivationService(
        IRepository<Employee> employeeRepository,
        IRepository<Project> projectRepository,
        IRepository<TaskItem> taskRepository,
        IRepository<EmployeeInvitation> invitationRepository,
        IRepository<ProjectEmployee> projectEmployeeRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService = null!)
    {
        _employeeRepository = employeeRepository;
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _invitationRepository = invitationRepository;
        _projectEmployeeRepository = projectEmployeeRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task<Result<AnalysisResultDto>> AnalyzeDeactivationAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employeeExists = await _employeeRepository.AnyAsync(e => e.Id == employeeId);
        if (!employeeExists)
            return Result<AnalysisResultDto>.Failure(new Error("Employee.NotFound", ErrorType.NotFound, "Employee not found."));

        var result = new AnalysisResultDto { IsAllowed = true };

        // 1. Check Active Tasks
        var activeTasks = await _taskRepository.GetQueryable()
            .Include(t => t.Sprint)
            .Where(t => t.EmployeeId == employeeId && 
                        t.Status != TaskItemStatus.Done && 
                        t.Sprint != null && 
                        t.Sprint.Status == SprintStatus.Active)
            .ToListAsync(ct);

        if (activeTasks.Any())
        {
            var taskBlock = new ActiveTasksBlock();
            taskBlock.Tasks = activeTasks.Select(t => new TaskRef { Title = t.TitleEn, Status = t.Status.ToString() }).ToList();
            result.Blocks.Add(taskBlock);
            result.IsAllowed = false;
        }

        // 2. Check Project Manager
        var managedProjects = await _projectRepository.GetQueryable()
            .Where(p => p.ManagerId == employeeId && p.Status != ProjectStatus.Completed && p.Status != ProjectStatus.Archived)
            .ToListAsync(ct);

        if (managedProjects.Any())
        {
            var managerBlock = new ProjectManagerBlock();
            managerBlock.ManagedProjects = managedProjects.Select(p => new ProjectRef { Name = p.NameEn }).ToList();
            result.Blocks.Add(managerBlock);
            result.IsAllowed = false;
        }

        // 3. Check Planned Tasks
        var plannedTasks = await _taskRepository.GetQueryable()
            .Include(t => t.Sprint).ThenInclude(s => s.Project)
            .Where(t => t.EmployeeId == employeeId && t.Sprint != null && t.Sprint.Status == SprintStatus.Planned)
            .ToListAsync(ct);

        if (plannedTasks.Any())
        {
            result.HasPlannedSprintTasks = true;
            result.AffectedSprints = plannedTasks
                .Where(t => t.Sprint != null && t.Sprint.Project != null)
                .GroupBy(t => t.Sprint)
                .Select(g => new AffectedSprintDto
                {
                    ProjectId = g.Key!.ProjectId,
                    ProjectName = g.Key.Project.NameEn,
                    SprintId = g.Key.Id,
                    SprintTitle = g.Key.TitleEn,
                    TaskCount = g.Count()
                }).ToList();
        }

        return Result<AnalysisResultDto>.Success(result);
    }

    public async Task<Result> DeactivateEmployeeAsync(Guid employeeId, DeactivateEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _employeeRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

        if (employee == null)
            return Result.Failure(new Error("Employee.NotFound", ErrorType.NotFound, "Employee not found."));

        if (employee.IsDeactivated)
            return Result.Failure(new Error("Employee.AlreadyDeactivated", ErrorType.Validation, "Employee is already deactivated."));

        var analysis = await AnalyzeDeactivationAsync(employeeId, ct);
        if (!analysis.IsSuccess || !analysis.Value.IsAllowed)
        {
            return Result.Failure(new Error("Employee.DeactivationBlocked", ErrorType.Validation, "Employee deactivation is blocked."));
        }

        employee.IsDeactivated = true;
        employee.DeactivationReason = request.Reason;
        employee.DeactivatedAt = DateTime.UtcNow;

        _employeeRepository.Update(employee);

        var plannedTasks = await _taskRepository.GetQueryable()
            .Include(t => t.Sprint)
            .Where(t => t.EmployeeId == employeeId && t.Sprint != null && t.Sprint.Status == SprintStatus.Planned)
            .ToListAsync(ct);

        foreach (var task in plannedTasks)
        {
            task.EmployeeId = null;
            _taskRepository.Update(task);
        }

        if (plannedTasks.Any() && _notificationService != null)
        {
            var affectedProjects = plannedTasks.Select(t => t.Sprint!.Project).Distinct();
            foreach (var project in affectedProjects)
            {
                if (project.ManagerId != Guid.Empty)
                {
                    await _notificationService.SendAsync(
                        userId: project.ManagerId,
                        type: NotificationType.EmployeeDeactivated,
                        messageEn: $"Employee '{employee.FirstNameEn}' was deactivated. Their tasks in the planned sprint are now unassigned.",
                        messageAr: $"تم إيقاف الموظف '{employee.FirstNameAr}'. مهامه في السبرينت المخطط أصبحت بدون تعيين.",
                        url: $"/projects/{project.Id}/board"
                    );
                }
            }
        }

        var projectEmployees = await _projectEmployeeRepository.GetQueryable()
            .Where(pe => pe.EmployeeId == employeeId)
            .ToListAsync(ct);

        foreach (var pe in projectEmployees)
        {
            pe.IsActive = false;
            _projectEmployeeRepository.Update(pe);
        }

        if (!string.IsNullOrEmpty(employee.Email))
        {
            var invitations = await _invitationRepository.GetQueryable()
                .Where(i => i.Email == employee.Email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(ct);

            foreach (var inv in invitations)
            {
                inv.ExpiresAt = DateTime.UtcNow;
                _invitationRepository.Update(inv);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReactivateEmployeeAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _employeeRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

        if (employee == null)
            return Result.Failure(new Error("Employee.NotFound", ErrorType.NotFound, "Employee not found."));

        if (!employee.IsDeactivated)
            return Result.Failure(new Error("Employee.AlreadyActive", ErrorType.Validation, "Employee is already active."));

        employee.IsDeactivated = false;
        employee.DeactivationReason = null;
        employee.DeactivatedAt = null;

        _employeeRepository.Update(employee);

        var projectEmployees = await _projectEmployeeRepository.GetQueryable()
            .Where(pe => pe.EmployeeId == employeeId)
            .ToListAsync(ct);

        foreach (var pe in projectEmployees)
        {
            pe.IsActive = true;
            _projectEmployeeRepository.Update(pe);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result> TerminateEmployeeAsync(Guid employeeId, TerminateEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _employeeRepository.GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);
            
        if (employee == null) 
            return Result.Failure(CommonErrors.NotFound("Employee"));

        // 1. Analyze first to ensure it's allowed (blocks if Active Sprint exists)
        var analysis = await AnalyzeDeactivationAsync(employeeId, ct);
        if (!analysis.IsSuccess || !analysis.Value.IsAllowed)
            return Result.Failure(new Error("TERMINATION_BLOCKED", ErrorType.Conflict, "Employee cannot be terminated due to active dependencies."));

        // 2. Perform deactivation cleanup if they are still active
        if (!employee.IsDeactivated)
        {
            var plannedTasks = await _taskRepository.GetQueryable()
                .Include(t => t.Sprint)
                .Where(t => t.EmployeeId == employeeId && t.Sprint != null && t.Sprint.Status == SprintStatus.Planned)
                .ToListAsync(ct);

            foreach (var task in plannedTasks)
            {
                task.EmployeeId = null;
                _taskRepository.Update(task);
            }

            var projectEmployees = await _projectEmployeeRepository.GetQueryable()
                .Where(pe => pe.EmployeeId == employeeId)
                .ToListAsync(ct);

            foreach (var pe in projectEmployees)
            {
                pe.IsActive = false;
                _projectEmployeeRepository.Update(pe);
            }

            if (!string.IsNullOrEmpty(employee.Email))
            {
                var invitations = await _invitationRepository.GetQueryable()
                    .Where(i => i.Email == employee.Email && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync(ct);

                foreach (var inv in invitations)
                {
                    inv.ExpiresAt = DateTime.UtcNow;
                    _invitationRepository.Update(inv);
                }
            }
        }

        // 3. Clear Company link completely
        employee.CompanyId = null;
        employee.IsDeactivated = false; // Reset to false as they are now a free agent
        employee.DeactivationReason = null;
        employee.DeactivatedAt = null;

        _employeeRepository.Update(employee);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
