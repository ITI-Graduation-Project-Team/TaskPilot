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

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/wbs")]
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
            
            var result = new
            {
                projectId = projectId,
                userStories = stories.Select(s => new
                {
                    id = s.Id,
                    titleEn = s.TitleEn,
                    titleAr = s.TitleAr,
                    priority = s.Priority.ToString(),
                    sprintId = s.SprintId,
                    tasks = s.Tasks?.Select(t => new
                    {
                        id = t.Id,
                        titleEn = t.TitleEn,
                        effortSize = t.EffortSize.ToString(),
                        type = t.Type.ToString(),
                        estimatedHours = t.EstimatedHours,
                        sprintId = t.SprintId
                    })
                })
            };

            return HandleResult(Result.Success<object>(result));
        }
    }
}
