using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SkillService : ISkillService
    {
        private readonly IRepository<Skill> _skillRepository;

        public SkillService(IRepository<Skill> skillRepository)
        {
            _skillRepository = skillRepository;
        }

        public async Task<Result<List<Skill>>> GetAllAsync()
        {
            var skills = await _skillRepository.GetAllAsync();

            return Result.Success(skills.ToList());
        }

        public async Task<Result<Skill>> CreateAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Result.Failure<Skill>(
                    CommonErrors.InvalidInput(
                        "Skill name is required"));
            }

            var normalizedName =
                SkillNormalizer.Normalize(name);

            var existingSkills =
                await _skillRepository.GetAllAsync();

            var existingSkill = existingSkills
                .FirstOrDefault(s =>
                    s.NormalizedName == normalizedName);

            // Skill already exists
            if (existingSkill != null &&
                !existingSkill.IsDeleted)
            {
                return Result.Failure<Skill>(
                    CommonErrors.Conflict(
                        "Skill already exists"));
            }

            // Restore soft deleted skill
            if (existingSkill != null &&
                existingSkill.IsDeleted)
            {
                existingSkill.IsDeleted = false;

                existingSkill.ModifiedAt =
                    DateTime.UtcNow;

                _skillRepository.Update(existingSkill);

                return Result.Success(existingSkill);
            }

            var skill = new Skill
            {
                Name = name.Trim(),

                NormalizedName = normalizedName
            };

            await _skillRepository.AddAsync(skill);

            return Result.Success(skill);
        }

        public async Task<Result> DeleteAsync(int id)
        {
            var skills = await _skillRepository
                .GetAllAsync();

            var skill = skills
                .FirstOrDefault(s => s.Id == id);

            if (skill == null)
            {
                return Result.Failure(
                    CommonErrors.NotFound("Skill"));
            }

            if (skill.IsDeleted)
            {
                return Result.Failure(
                    CommonErrors.Conflict(
                        "Skill already deleted"));
            }

            skill.IsDeleted = true;

            skill.ModifiedAt = DateTime.UtcNow;

            _skillRepository.Update(skill);

            return Result.Success();
        }

        public async Task<Result<List<Skill>>> CreateBulkAsync(
            List<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return Result.Failure<List<Skill>>(
                    CommonErrors.InvalidInput(
                        "Skills list is empty"));
            }

            var normalizedInput = names
                .Where(n =>
                    !string.IsNullOrWhiteSpace(n))
                .Select(n => new
                {
                    OriginalName = n.Trim(),

                    NormalizedName =
                        SkillNormalizer.Normalize(n)
                })
                .DistinctBy(x => x.NormalizedName)
                .ToList();

            var normalizedNames = normalizedInput
                .Select(x => x.NormalizedName)
                .ToList();

            var existingSkills =
                await _skillRepository.FindAsync(
                    s => normalizedNames.Contains(
                        s.NormalizedName));

            var resultSkills = new List<Skill>();

            foreach (var item in normalizedInput)
            {
                var existingSkill = existingSkills
                    .FirstOrDefault(s =>
                        s.NormalizedName ==
                        item.NormalizedName);

                // Existing active skill
                if (existingSkill != null &&
                    !existingSkill.IsDeleted)
                {
                    resultSkills.Add(existingSkill);

                    continue;
                }

                // Restore deleted skill
                if (existingSkill != null &&
                    existingSkill.IsDeleted)
                {
                    existingSkill.IsDeleted = false;

                    existingSkill.ModifiedAt =
                        DateTime.UtcNow;

                    _skillRepository.Update(
                        existingSkill);

                    resultSkills.Add(existingSkill);

                    continue;
                }

                // Create new skill
                var newSkill = new Skill
                {
                    Name = item.OriginalName,

                    NormalizedName =
                        item.NormalizedName
                };

                await _skillRepository
                    .AddAsync(newSkill);

                resultSkills.Add(newSkill);
            }

            return Result.Success(resultSkills);
        }
    }
}