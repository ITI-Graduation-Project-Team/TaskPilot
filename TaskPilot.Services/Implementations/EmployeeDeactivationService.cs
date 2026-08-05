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
    private readonly IUnitOfWork _unitOfWork;

    public EmployeeDeactivationService(
        IRepository<Employee> employeeRepository,
        IRepository<Project> projectRepository,
        IRepository<TaskItem> taskRepository,
        IRepository<EmployeeInvitation> invitationRepository,
        IUnitOfWork unitOfWork)
    {
        _employeeRepository = employeeRepository;
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _invitationRepository = invitationRepository;
        _unitOfWork = unitOfWork;
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
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}
