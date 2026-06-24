using Microsoft.AspNetCore.Mvc;
using TaskPilot.AI.Models.ContextAdvisor;
using TaskPilot.AI.Orchestrators;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Presentation.Controllers
{
    [ApiController]
    [Route("api/context-advisor")]
    public class ContextAdvisorController : ApiControllerBase
    {
        private readonly ContextAdvisorOrchestrator _contextAdvisorOrchestrator;
        private readonly DocumentIngestionOrchestrator _documentIngestionOrchestrator;
        private readonly IRepository<TaskItem> _taskRepository;

        public ContextAdvisorController(
            ContextAdvisorOrchestrator contextAdvisorOrchestrator,
            DocumentIngestionOrchestrator documentIngestionOrchestrator,
            IRepository<TaskItem> taskRepository)
        {
            _contextAdvisorOrchestrator = contextAdvisorOrchestrator;
            _documentIngestionOrchestrator = documentIngestionOrchestrator;
            _taskRepository = taskRepository;
        }

        [HttpPost("documents")]
        public async Task<IActionResult> UploadProjectKnowledge(
            [FromForm] ProjectKnowledgeUploadRequest request,
            CancellationToken cancellationToken)
        {
            var result =
                await _documentIngestionOrchestrator
                    .IngestProjectKnowledgeAsync(
                        request.File,
                        request.ProjectId,
                        request.IsAvailableToContextSummarizer,
                        cancellationToken);

            return Ok(result);
        }

        [HttpPost("summary")]
        public async Task<IActionResult> GetContextSummary(
            [FromBody] ContextAdvisorSummaryRequest request,
            CancellationToken cancellationToken)
        {
            var taskItem = await _taskRepository.GetByIdAsync(request.TaskId, t => t.Sprint);
            if (taskItem is null)
            {
                return HandleResult(Result.Failure(CommonErrors.NotFound("Task")));
            }

            bool isArabic = Localizer.CurrentLanguage == "ar";

            var downstreamRequest = new TaskContextRequest
            {
                ProjectId = taskItem.Sprint?.ProjectId,
                TaskId = taskItem.Id,
                TaskTitle = isArabic ? taskItem.TitleAr : taskItem.TitleEn,
                TaskDescription = isArabic ? taskItem.DescriptionAr : taskItem.DescriptionEn,
                AcceptanceCriteria = isArabic ? taskItem.AcceptanceCriteriaAr : taskItem.AcceptanceCriteriaEn,
                TechnicalSummary = isArabic ? taskItem.TechnicalSummaryAr : taskItem.TechnicalSummaryEn,
                RelatedPastTasks = new List<string>(), // AI mapping expectation - defaults to empty if task model doesn't supply it
                TopK = 6
            };

            var result =
                await _contextAdvisorOrchestrator
                    .GenerateSummaryAsync(downstreamRequest, cancellationToken);

            return Ok(result);
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask(
            [FromBody] ContextAdvisorAskRequest request,
            CancellationToken cancellationToken)
        {
            var taskItem = await _taskRepository.GetByIdAsync(request.TaskId, t => t.Sprint);
            if (taskItem is null)
            {
                return HandleResult(Result.Failure(CommonErrors.NotFound("Task")));
            }

            bool isArabic = Localizer.CurrentLanguage == "ar";

            var downstreamRequest = new ContextAdvisorChatRequest
            {
                ProjectId = taskItem.Sprint?.ProjectId,
                TaskId = taskItem.Id,
                TaskTitle = isArabic ? taskItem.TitleAr : taskItem.TitleEn,
                TaskDescription = isArabic ? taskItem.DescriptionAr : taskItem.DescriptionEn,
                AcceptanceCriteria = isArabic ? taskItem.AcceptanceCriteriaAr : taskItem.AcceptanceCriteriaEn,
                TechnicalSummary = isArabic ? taskItem.TechnicalSummaryAr : taskItem.TechnicalSummaryEn,
                RelatedPastTasks = new List<string>(), // AI mapping expectation
                TopK = 6,
                ConversationId = request.ConversationId,
                Question = request.Question
            };

            var result =
                await _contextAdvisorOrchestrator
                    .AskAsync(downstreamRequest, cancellationToken);

            return Ok(result);
        }
    }
}
