using Microsoft.AspNetCore.Authorization;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Agents.Planning;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Services.DTOs;
using TaskPilot.DTOs.Projects;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/wbs")]
    [Authorize(Roles = "ProjectManager")]
    public class WbsController : ApiControllerBase
    {
        private readonly IWbsGenerationService _wbsGenerationService;
        private readonly IWbsSkillEnrichmentService _wbsSkillEnrichmentService;
        private readonly IUserStoryRepository _userStoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public WbsController(
            IWbsGenerationService wbsGenerationService,
            IWbsSkillEnrichmentService wbsSkillEnrichmentService,
            IUserStoryRepository userStoryRepository,
            IUnitOfWork unitOfWork)
        {
            _wbsGenerationService = wbsGenerationService;
            _wbsSkillEnrichmentService = wbsSkillEnrichmentService;
            _userStoryRepository = userStoryRepository;
            _unitOfWork = unitOfWork;
        }

        [HttpPost("generate")]
        public async Task<ActionResult> Generate(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _wbsGenerationService.GenerateAsync(projectId, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result);
        }

        [HttpPost("enrich-skills")]
        public async Task<ActionResult> EnrichSkills(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _wbsSkillEnrichmentService.EnrichProjectTasksAsync(projectId, cancellationToken);
            if (result.IsSuccess)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            return HandleResult(result, TaskPilot.Models.Common.SuccessCodes.Wbs.SkillsEnriched);
        }

        [HttpGet]
        public async Task<ActionResult> Get(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var stories = await _userStoryRepository.GetByProjectIdAsync(projectId, cancellationToken);
            
            var result = new WbsDto
            {
                ProjectId = projectId,
                UserStories = stories.Select(s => new WbsUserStoryDto
                {
                    Id = s.Id,
                    TitleEn = s.TitleEn,
                    TitleAr = s.TitleAr,
                    Priority = s.Priority.ToString(),
                    SprintId = s.SprintId,
                    Tasks = s.Tasks?.Select(t => new WbsTaskDto
                    {
                        Id = t.Id,
                        TitleEn = t.TitleEn,
                        EffortSize = t.EffortSize.ToString(),
                        Type = t.Type.ToString(),
                        EstimatedHours = t.EstimatedHours,
                        SprintId = t.SprintId
                    })
                })
            };

            return HandleResult(Result.Success(result));
        }
    }
}
