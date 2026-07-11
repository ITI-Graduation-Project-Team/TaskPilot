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
        private readonly IRepository<User> _userRepository;
        private readonly IRepository<Employee> _employeeRepository;
        private readonly IFileTextExtractor _fileExtractor;
        private readonly ICvAiService _cvAiService;

        public CvService(
            IRepository<User> userRepository,
            IRepository<Employee> employeeRepository,
            IFileTextExtractor fileExtractor,
            ICvAiService cvAiService)
        {
            _userRepository = userRepository;
            _employeeRepository = employeeRepository;
            _fileExtractor = fileExtractor;
            _cvAiService = cvAiService;
        }

        public async Task<Result<ParsedCvDto>> ExtractAsync(
                Guid userId,
                IFormFile file)
        {
            var userExists = await _userRepository.AnyAsync(u => u.Id == userId);

            if (!userExists)
            {
                return Result.Failure<ParsedCvDto>(UserErrors.NotFound);
            }

            var text = await _fileExtractor.ExtractTextAsync(file);

            if (string.IsNullOrWhiteSpace(text))
            {
                return Result.Failure<ParsedCvDto>(CvErrors.InvalidFile);
            }

            var employee = await _employeeRepository.FindSingleAsync(e => e.Id == userId);

            if (employee is null)
            {
                return Result.Failure<ParsedCvDto>(UserErrors.NotFound);
            }

            employee.CvProcessingStatus = AiProcessingStatus.Processing;

            try
            {
                var parsedCv = await _cvAiService.ParseCvAsync(text);
                parsedCv.Skills ??= new List<ParsedSkillDto>();

                employee.CvProcessingStatus = AiProcessingStatus.Completed;

                return Result.Success(parsedCv);
            }
            catch (Exception)
            {
                employee.CvProcessingStatus = AiProcessingStatus.Failed;
                return Result.Failure<ParsedCvDto>(CvErrors.ExtractionFailed);
            }
        }
    }
}