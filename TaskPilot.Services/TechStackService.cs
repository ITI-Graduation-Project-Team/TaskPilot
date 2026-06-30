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

        public async Task<TechStackSuggestion> SuggestAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project is null)
                throw new KeyNotFoundException("Project not found.");

            if (project.RequirementsSnapshot is null)
                throw new InvalidOperationException("RequirementsSnapshot is missing.");

            // 2. Load company employee skills
            var skills = await _skillRepository.GetCompanySkillSummaryAsync(project.CompanyId, cancellationToken);

            // 3. Call agent with both inputs
            return await _techStackAdvisorAgent.SuggestAsync(
                project.RequirementsSnapshot,
                skills,
                cancellationToken);
        }

        public async Task ConfirmAsync(
            Guid projectId,
            ConfirmTechStackRequest request,
            CancellationToken cancellationToken = default)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project is null)
                throw new KeyNotFoundException("Project not found.");

            project.TechStack = request.TechStack;
            project.PlatformTargets = request.PlatformTargets;
            project.ProjectType = request.ProjectType;

            _projectRepository.Update(project);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
