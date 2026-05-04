using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Context;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public class SkillService : ISkillService
    {
        private readonly ApplicationDbContext _context;

        public SkillService(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<Result<List<Skill>>> GetAllAsync()
        {
            var skills = await _context.Skills
                .AsNoTracking()
                .ToListAsync();

            return Result.Success(skills);
        }
        public async Task<Result<Skill>> CreateAsync(string name)
        {
            var normalized = Normalize(name);

            var exists = await _context.Skills
                .IgnoreQueryFilters() 
                .FirstOrDefaultAsync(s => s.Name.ToLower() == normalized);

            if (exists != null && !exists.IsDeleted)
                return Result.Failure<Skill>(CommonErrors.Conflict("Skill already exists"));

            if (exists != null && exists.IsDeleted)
            {
                exists.IsDeleted = false;
                exists.ModifiedAt = DateTime.UtcNow;

                return Result.Success(exists);
            }

            var skill = new Skill
            {
                Name = name.Trim()
            };

            await _context.Skills.AddAsync(skill);

            return Result.Success(skill);
        }

        public async Task<Result> DeleteAsync(int id)
        {
            var skill = await _context.Skills.FindAsync(id);

            if (skill == null)
                return Result.Failure(CommonErrors.NotFound("Skill"));

            if (skill.IsDeleted)
                return Result.Failure(CommonErrors.Conflict("Skill already deleted"));

            skill.IsDeleted = true;
            skill.ModifiedAt = DateTime.UtcNow;

            return Result.Success();
        }
        public async Task<Result<List<Skill>>> CreateBulkAsync(List<string> names)
        {
            if (names == null || names.Count == 0)
                return Result.Failure<List<Skill>>(CommonErrors.InvalidInput("Skills list is empty"));

            var normalizedInput = names
                .Select(n => Normalize(n))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .ToList();

            var existingSkills = await _context.Skills
                .IgnoreQueryFilters()
                .Where(s => normalizedInput.Contains(s.Name.ToLower()))
                .ToListAsync();

            var resultSkills = new List<Skill>();

            foreach (var name in normalizedInput)
            {
                var existing = existingSkills
                    .FirstOrDefault(s => s.Name.ToLower() == name);

                if (existing != null)
                {
                    if (existing.IsDeleted)
                    {
                        existing.IsDeleted = false;
                        existing.ModifiedAt = DateTime.UtcNow;
                    }

                    resultSkills.Add(existing);
                }
                else
                {
                    var newSkill = new Skill
                    {
                        Name = name
                    };

                    resultSkills.Add(newSkill);
                }
            }

            var newOnes = resultSkills.Where(s => s.Id == 0).ToList();

            if (newOnes.Any())
                await _context.Skills.AddRangeAsync(newOnes);

            return Result.Success(resultSkills);
        }
        private string Normalize(string input)
        {
            return input.Trim().ToLower();
        }
    }
}