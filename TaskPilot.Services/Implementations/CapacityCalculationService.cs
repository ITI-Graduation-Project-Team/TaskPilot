using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class CapacityCalculationService : ICapacityCalculationService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Company> _companyRepository;
        private readonly IProjectEmployeeRepository _projectEmployeeRepository;

        public CapacityCalculationService(
            IRepository<Project> projectRepository,
            IRepository<Company> companyRepository,
            IProjectEmployeeRepository projectEmployeeRepository)
        {
            _projectRepository = projectRepository;
            _companyRepository = companyRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
        }

        public async Task<Result<SprintCapacityResult>> CalculateTargetSprintHoursAsync(Guid projectId, DateTime sprintStartDate, DateTime sprintEndDate, CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project == null)
            {
                return Result.Failure<SprintCapacityResult>(CommonErrors.NotFound("Project"));
            }

            var company = await _companyRepository.GetByIdAsync(project.CompanyId);
            if (company == null)
            {
                return Result.Failure<SprintCapacityResult>(CommonErrors.NotFound("Company"));
            }

            var activeEmployees = await _projectEmployeeRepository.GetActiveByProjectIdAsync(projectId, cancellationToken);

            int sprintWorkingDays = CountWorkingDays(sprintStartDate, sprintEndDate, (WorkingDays)company.WorkingDaysMask);

            decimal teamCapacityHours = 0;
            foreach (var pe in activeEmployees)
            {
                decimal employeeSprintHours = company.WorkingHoursPerDay * sprintWorkingDays * (pe.AllocationPercentage / 100m);
                teamCapacityHours += employeeSprintHours;
            }

            decimal targetSprintHours = teamCapacityHours * company.DefaultCapacityBufferPercentage;
            
            var bufferPercentInt = (int)(company.DefaultCapacityBufferPercentage * 100);
            var explanationEn = $"{activeEmployees.Count} employees × {sprintWorkingDays} working days × {company.WorkingHoursPerDay} hours × {bufferPercentInt}% buffer = {Math.Round(targetSprintHours, 1)} hours";
            var explanationAr = $"{activeEmployees.Count} موظفين × {sprintWorkingDays} أيام عمل × {company.WorkingHoursPerDay} ساعات × {bufferPercentInt}% نسبة الأمان = {Math.Round(targetSprintHours, 1)} ساعة";

            var capacityResult = new SprintCapacityResult
            {
                TargetSprintHours = targetSprintHours,
                ExplanationEn = explanationEn,
                ExplanationAr = explanationAr
            };

            return Result.Success(capacityResult);
        }

        private int CountWorkingDays(DateTime start, DateTime end, WorkingDays workingDays)
        {
            int count = 0;
            for (var date = start.Date; date <= end.Date; date = date.AddDays(1))
            {
                var flag = MapDayOfWeekToFlag(date.DayOfWeek);
                if (workingDays.HasFlag(flag))
                {
                    count++;
                }
            }
            return count;
        }

        private WorkingDays MapDayOfWeekToFlag(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Sunday => WorkingDays.Sunday,
                DayOfWeek.Monday => WorkingDays.Monday,
                DayOfWeek.Tuesday => WorkingDays.Tuesday,
                DayOfWeek.Wednesday => WorkingDays.Wednesday,
                DayOfWeek.Thursday => WorkingDays.Thursday,
                DayOfWeek.Friday => WorkingDays.Friday,
                DayOfWeek.Saturday => WorkingDays.Saturday,
                _ => WorkingDays.None
            };
        }
    }
}
