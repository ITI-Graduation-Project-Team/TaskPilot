using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.AI.Models.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;

namespace TaskPilot.Services
{
    public class TechStackService : ITechStackService
    {
        private readonly IRepository<Project> _projectRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TechStackAdvisorAgent _techStackAdvisorAgent;
        private readonly ISkillRepository _skillRepository;

        public TechStackService(
            IRepository<Project> projectRepository,
            IUnitOfWork unitOfWork,
            TechStackAdvisorAgent techStackAdvisorAgent,
            ISkillRepository skillRepository)
        {
            _projectRepository = projectRepository;
            _unitOfWork = unitOfWork;
            _techStackAdvisorAgent = techStackAdvisorAgent;
            _skillRepository = skillRepository;
        }

        public async Task<Result<TechStackSuggestion>> SuggestAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project is null)
                return Result.Failure<TechStackSuggestion>(CommonErrors.NotFound("Project"));

            if (project.RequirementsSnapshot is null)
                return Result.Failure<TechStackSuggestion>(CommonErrors.InvalidInput("RequirementsSnapshot is missing."));

            // 2. Load company employee skills
            var skills = await _skillRepository.GetCompanySkillSummaryAsync(project.CompanyId, cancellationToken);

            // 3. Call agent with both inputs
            var suggestion = await _techStackAdvisorAgent.SuggestAsync(
                project.RequirementsSnapshot,
                skills,
                cancellationToken);

            return Result.Success(suggestion);
        }

        public async Task<Result> ConfirmAsync(
            Guid projectId,
            ConfirmTechStackRequest request,
            CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project is null)
                return Result.Failure(CommonErrors.NotFound("Project"));

            project.TechStack = request.TechStack;
            project.PlatformTargets = request.PlatformTargets;
            project.ProjectType = request.ProjectType;

            _projectRepository.Update(project);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
