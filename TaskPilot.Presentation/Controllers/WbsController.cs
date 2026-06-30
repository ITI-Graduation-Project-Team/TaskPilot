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
        private readonly IUserStoryRepository _userStoryRepository;

        public WbsController(
            IWbsGenerationService wbsGenerationService,
            IUserStoryRepository userStoryRepository)
        {
            _wbsGenerationService = wbsGenerationService;
            _userStoryRepository = userStoryRepository;
        }

        [HttpPost("generate")]
        public async Task<ActionResult> Generate(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var result = await _wbsGenerationService.GenerateAsync(projectId, cancellationToken);
            return HandleResult(result);
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

            return Ok(result);
        }
    }
}
