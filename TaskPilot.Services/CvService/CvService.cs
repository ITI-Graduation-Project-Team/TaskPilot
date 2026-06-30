using Microsoft.AspNetCore.Http;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.CV;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Helpers;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

namespace TaskPilot.Services
{
    public class CvService : ICvService
    {
        private readonly IRepository<User>
            _userRepository;

        private readonly IRepository<Employee>
            _employeeRepository;

        private readonly IRepository<Skill>
            _skillRepository;

        private readonly IRepository<UserSkill>
            _userSkillRepository;

        private readonly IFileTextExtractor
            _fileExtractor;

        private readonly ICvAiService
                _cvAiService;

        public CvService(
            IRepository<User> userRepository,
            IRepository<Employee> employeeRepository,
            IRepository<Skill> skillRepository,
            IRepository<UserSkill> userSkillRepository,
            IFileTextExtractor fileExtractor,
            ICvAiService cvAiService)
        {
            _userRepository = userRepository;

            _employeeRepository =
                employeeRepository;

            _skillRepository =
                skillRepository;

            _userSkillRepository =
                userSkillRepository;

            _fileExtractor =
                fileExtractor;

            _cvAiService = cvAiService;
        }

        public async Task<Result<ParsedCvDto>>
            ProcessCvAsync(
                Guid userId,
                IFormFile file)
        {
            // Validate User

            var userExists =
                await _userRepository
                    .AnyAsync(u =>
                        u.Id == userId);

            if (!userExists)
            {
                return Result
                    .Failure<ParsedCvDto>(
                        CommonErrors
                            .NotFound("User"));
            }

            // Extract Text

            var text =
                await _fileExtractor
                    .ExtractTextAsync(file);

            if (string.IsNullOrWhiteSpace(text))
            {
                return Result
                    .Failure<ParsedCvDto>(
                        CommonErrors
                            .InvalidInput(
                                "CV is empty"));
            }

            // Get Employee

            var employee =
                await _employeeRepository
                    .FindSingleAsync(e =>
                        e.Id == userId);

            if (employee is null)
            {
                return Result
                    .Failure<ParsedCvDto>(
                        CommonErrors
                            .NotFound("Employee"));
            }

            employee.CvProcessingStatus =
                AiProcessingStatus.Processing;

            try
            {
                // Parse CV Using AI

                var parsedCv =
                    await _cvAiService.ParseCvAsync(text);

                parsedCv.Skills ??=new List<ParsedSkillDto>();

                employee.JobTitle =
                    parsedCv.JobTitle;

                employee.SeniorityLevel =
                    parsedCv.SeniorityLevel;

                employee.TotalYearsOfExperience =
                    parsedCv
                        .TotalYearsOfExperience;

                // Normalize Skills

                var normalizedSkills =
                    parsedCv.Skills
                        .Where(s =>
                            !string
                                .IsNullOrWhiteSpace(
                                    s.Name))
                        .Select(s => new
                        {
                            ParsedSkill = s,

                            NormalizedName =
                                SkillNormalizer
                                    .Normalize(
                                        s.Name)
                        })
                        .Where(x =>
                            !string
                                .IsNullOrWhiteSpace(
                                    x.NormalizedName))
                        .DistinctBy(x =>
                            x.NormalizedName)
                        .ToList();

                // Extract Skill Names

                var normalizedNames =
                    normalizedSkills
                        .Select(x =>
                            x.NormalizedName)
                        .ToList();

                // Existing Skills

                var existingSkills =
                    await _skillRepository
                        .FindAsync(s =>
                            normalizedNames
                                .Contains(
                                    s.NormalizedName));

                var skillDictionary =
                    existingSkills
                        .ToDictionary(
                            s => s.NormalizedName);

                // Remove Old User Skills

                var existingUserSkills =
                    await _userSkillRepository
                        .FindAsync(us =>
                            us.UserId == userId);

                _userSkillRepository
                    .DeleteRange(
                        existingUserSkills);

                // Add Fresh Skills

                foreach (var item
                         in normalizedSkills)
                {
                    if (!skillDictionary
                            .TryGetValue(
                                item.NormalizedName,
                                out var skill))
                    {
                        skill = new Skill
                        {
                            Name =
                                item
                                    .ParsedSkill
                                    .Name
                                    .Trim(),

                            NormalizedName =
                                item.NormalizedName
                        };

                        await _skillRepository
                            .AddAsync(skill);

                        skillDictionary[
                            item.NormalizedName]
                            = skill;
                    }

                    await _userSkillRepository
                        .AddAsync(
                            new UserSkill
                            {
                                UserId = userId,

                                Skill = skill,

                                Level =
                                    item
                                        .ParsedSkill
                                        .Level
                                    ??
                                    SkillLevel
                                        .Intermediate,

                                YearsOfExperience =
                                    item
                                        .ParsedSkill
                                        .YearsOfExperience,

                                ConfidenceScore =
                                    item
                                        .ParsedSkill
                                        .ConfidenceScore,

                                IsPrimary = false
                            });
                }

                // Processing Completed

                employee.CvProcessingStatus =
                    AiProcessingStatus.Completed;

                employee.IsProfileCompleted =
                    true;

                employee.LastCvProcessedAt =
                    DateTime.UtcNow;

                return Result
                    .Success(parsedCv);
            }
            catch (Exception)
            {
                employee.CvProcessingStatus =
                    AiProcessingStatus.Failed;

                return Result
                    .Failure<ParsedCvDto>(
                        CommonErrors
                            .OperationFailed(
                                "Failed to process CV."));
            }
        }
    }
}