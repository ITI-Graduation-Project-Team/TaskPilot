using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Context;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

namespace TaskPilot.Services
{
    public class CvService : ICvService
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileTextExtractor _fileExtractor;
        private readonly ICvParserService _cvParser;

        public CvService(
            ApplicationDbContext context,
            IFileTextExtractor fileExtractor,
            ICvParserService cvParser)
        {
            _context = context;
            _fileExtractor = fileExtractor;
            _cvParser = cvParser;
        }

        public async Task<Result<List<string>>> ProcessCvAsync(Guid userId, IFormFile file)
        {
            var userExists = await _context.Users
                .AnyAsync(u => u.Id == userId);

            if (!userExists)
                return Result.Failure<List<string>>(CommonErrors.NotFound("User"));

            var text = await _fileExtractor.ExtractTextAsync(file);

            if (string.IsNullOrWhiteSpace(text))
                return Result.Failure<List<string>>(CommonErrors.InvalidInput("CV is empty"));

            var extractedSkills = await _cvParser.ExtractSkillsAsync(text);

            if (extractedSkills == null || extractedSkills.Count == 0)
                return Result.Success(new List<string>());

            // 🟢 4. Normalize skills
            var normalizedSkills = extractedSkills
                .Select(s => s.Trim().ToLower())
                .Distinct()
                .ToList();

            var dbSkills = await _context.Skills
                .Where(s => normalizedSkills.Contains(s.Name.ToLower()))
                .ToListAsync();

            foreach (var skill in dbSkills)
            {
                var exists = await _context.UserSkills
                    .AnyAsync(us => us.UserId == userId && us.SkillId == skill.Id);

                if (!exists)
                {
                    _context.UserSkills.Add(new UserSkill
                    {
                        UserId = userId,
                        SkillId = skill.Id
                    });
                }
            }


            return Result.Success(extractedSkills);
        }
    }
}