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
using TaskPilot.Presentation.Models;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId}/wbs")]
    [Authorize(Roles = "ProjectManager")]
    public class WbsController : ApiControllerBase
    {
        private readonly IProjectSetupService _projectSetupService;
        private readonly IUserStoryRepository _userStoryRepository;

        public WbsController(
            IProjectSetupService projectSetupService,
            IUserStoryRepository userStoryRepository)
        {
            _projectSetupService = projectSetupService;
            _userStoryRepository = userStoryRepository;
        }

        [HttpPost("generate")]
        public async Task<ActionResult> Generate(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            return await QueueGeneration(projectId, cancellationToken);
        }

        [HttpPost("generation")]
        public async Task<ActionResult> QueueGeneration(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _projectSetupService.QueueWbsAsync(projectId, cancellationToken);
            return result.IsSuccess
                ? Accepted(ApiResponse.Success(result.Value, "WBS generation was queued."))
                : HandleResult(result);
        }

        [HttpPost("enrich-skills")]
        public async Task<ActionResult> EnrichSkills(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            return await QueueSkillEnrichment(projectId, cancellationToken);
        }

        [HttpPost("skills-enrichment")]
        public async Task<ActionResult> QueueSkillEnrichment(Guid projectId, CancellationToken cancellationToken)
        {
            var result = await _projectSetupService.QueueSkillsAsync(projectId, cancellationToken);
            return result.IsSuccess
                ? Accepted(ApiResponse.Success(result.Value, "Skill enrichment was queued."))
                : HandleResult(result);
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
