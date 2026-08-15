using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.CV;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Helpers;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

namespace TaskPilot.Services
{
    public class CvConfirmationService : ICvConfirmationService
    {
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Employee> _employeeRepository;
        private readonly IRepository<Skill> _skillRepository;
        private readonly IRepository<UserSkill> _userSkillRepository;

        public CvConfirmationService(
            IRepository<User> userRepository,
            IRepository<Employee> employeeRepository,
            IRepository<Skill> skillRepository,
            IRepository<UserSkill> userSkillRepository)
        {
            _userRepository = userRepository;
            _employeeRepository = employeeRepository;
            _skillRepository = skillRepository;
            _userSkillRepository = userSkillRepository;
        }

        public async Task<Result> ConfirmAsync(Guid userId, ConfirmCvRequest request)
        {
            if (request.Skills == null || !request.Skills.Any())
            {
                return Result.Failure(CvErrors.NoSkillsSelected);
            }

            if (request.Skills.Any(s => string.IsNullOrWhiteSpace(s.Name)))
            {
                return Result.Failure(CvErrors.InvalidSkillName);
            }

            if (request.TotalYearsOfExperience.HasValue && request.TotalYearsOfExperience < 0)
            {
                return Result.Failure(CvErrors.NegativeExperience);
            }

            if (request.Skills.Any(s => s.YearsOfExperience < 0))
            {
                return Result.Failure(CvErrors.NegativeExperience);
            }

            if (string.IsNullOrWhiteSpace(request.JobTitle))
            {
                return Result.Failure(CvErrors.NullJobTitle);
            }

            var primarySkillsCount = request.Skills.Count(s => s.IsPrimary);
            if (primarySkillsCount == 0)
            {
                return Result.Failure(CvErrors.PrimarySkillRequired);
            }
            if (primarySkillsCount > 1)
            {
                return Result.Failure(CvErrors.MultiplePrimarySkills);
            }

            var duplicateSkills = request.Skills
                .GroupBy(s => SkillNormalizer.Normalize(s.Name))
                .Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key))
                .Any();

            if (duplicateSkills)
            {
                return Result.Failure(CvErrors.DuplicateSkills);
            }

            var userExists = await _userRepository.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return Result.Failure(UserErrors.NotFound);
            }

            var employee = await _employeeRepository.FindSingleAsync(e => e.Id == userId);
            if (employee is null)
            {
                return Result.Failure(UserErrors.NotFound);
            }

            employee.JobTitle = request.JobTitle;
            employee.SeniorityLevel = request.SeniorityLevel;
            employee.TotalYearsOfExperience = (int?)request.TotalYearsOfExperience;

            var normalizedSkills = request.Skills
                .Select(s => new
                {
                    Dto = s,
                    NormalizedName = SkillNormalizer.Normalize(s.Name)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.NormalizedName))
                .DistinctBy(x => x.NormalizedName)
                .ToList();

            var normalizedNames = normalizedSkills.Select(x => x.NormalizedName).ToList();

            var existingSkills = await _skillRepository.GetQueryable()
                .IgnoreQueryFilters()
                .Where(s => normalizedNames.Contains(s.NormalizedName))
                .ToListAsync();
            var skillDictionary = existingSkills.ToDictionary(s => s.NormalizedName, StringComparer.OrdinalIgnoreCase);

            var existingUserSkills = await _userSkillRepository.FindAsync(us => us.UserId == userId);
            _userSkillRepository.DeleteRange(existingUserSkills);

            foreach (var item in normalizedSkills)
            {
                if (!skillDictionary.TryGetValue(item.NormalizedName, out var skill))
                {
                    skill = new Skill
                    {
                        Name = item.Dto.Name.Trim(),
                        NormalizedName = item.NormalizedName
                    };

                    await _skillRepository.AddAsync(skill);
                    skillDictionary[item.NormalizedName] = skill;
                }
                else if (skill.IsDeleted)
                {
                    skill.IsDeleted = false;
                    _skillRepository.Update(skill);
                }

                await _userSkillRepository.AddAsync(new UserSkill
                {
                    UserId = userId,
                    Skill = skill,
                    Level = item.Dto.Level,
                    YearsOfExperience = (double)item.Dto.YearsOfExperience,
                    IsPrimary = item.Dto.IsPrimary
                });
            }

            employee.IsProfileCompleted = true;
            employee.LastCvProcessedAt = DateTime.UtcNow;

            return Result.Success();
        }
    }
}
