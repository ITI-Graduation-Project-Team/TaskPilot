using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskPilot.AI.Agents;
using TaskPilot.AI.Exceptions;
using TaskPilot.Data.Repositories;
using TaskPilot.DTOs.AI.AgileCoach;
using TaskPilot.DTOs.AgileCoach;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.AgileCoach;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class AgileCoachService : IAgileCoachService
    {
        private readonly IRepository<TaskItem> _taskRepository;
        private readonly IRepository<AgileCoachChatMessage> _chatMessageRepository;
        private readonly IRepository<ProjectChatSession> _chatSessionRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly AgileCoachAgent _agileCoachAgent;

        public AgileCoachService(
            IRepository<TaskItem> taskRepository,
            IRepository<AgileCoachChatMessage> chatMessageRepository,
            IRepository<ProjectChatSession> chatSessionRepository,
            ICurrentUserService currentUserService,
            AgileCoachAgent agileCoachAgent)
        {
            _taskRepository = taskRepository;
            _chatMessageRepository = chatMessageRepository;
            _chatSessionRepository = chatSessionRepository;
            _currentUserService = currentUserService;
            _agileCoachAgent = agileCoachAgent;
        }

        private async Task<Result<TaskItem>> GetAndValidateTaskOwnershipAsync(Guid taskId, CancellationToken cancellationToken)
        {
            var task = await _taskRepository.GetQueryable()
                .Include(t => t.UserStory)
                    .ThenInclude(us => us.Project)
                        .ThenInclude(p => p.ProjectEmployees)
                .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

            if (task == null)
            {
                return Result.Failure<TaskItem>(CommonErrors.NotFound("TaskItem"));
            }

            var project = task.UserStory?.Project;
            if (project == null)
            {
                return Result.Failure<TaskItem>(CommonErrors.InvalidInput("Task is not associated with a project."));
            }

            var currentUserId = _currentUserService.UserId;
            if (currentUserId == null)
            {
                return Result.Failure<TaskItem>(CommonErrors.Unauthorized());
            }

            var isAssigned = task.EmployeeId == currentUserId;
            var isManager = project.ManagerId == currentUserId;
            var isParticipant = project.ProjectEmployees?.Any(pe => pe.EmployeeId == currentUserId) ?? false;

            if (!isAssigned && !isManager && !isParticipant)
            {
                return Result.Failure<TaskItem>(CommonErrors.Forbidden("You do not have access to this project."));
            }

            if (project.RequirementsSessionId == null)
            {
                return Result.Failure<TaskItem>(CommonErrors.InvalidInput("Project requirements session ID is missing. Cannot retrieve context."));
            }

            return Result.Success(task);
        }

        public async Task<Result<AgileCoachSummaryServiceResult>> GetOrGenerateSummaryAsync(Guid taskId, string lang, CancellationToken cancellationToken = default)
        {
            var taskResult = await GetAndValidateTaskOwnershipAsync(taskId, cancellationToken);
            if (!taskResult.IsSuccess) return Result.Failure<AgileCoachSummaryServiceResult>(taskResult.Error!);

            var task = taskResult.Value!;
            
            if (task.TechnicalSummaryEn != null)
            {
                var content = lang == "ar" ? task.TechnicalSummaryAr : task.TechnicalSummaryEn;

                var responseSummary = new AgileCoachSummaryResponse
                {
                    Id = task.Id,
                    TaskItemId = task.Id,
                    Content = content ?? string.Empty,
                    GeneratedAt = DateTime.UtcNow,
                    IsNewlyGenerated = false
                };

                return Result.Success(new AgileCoachSummaryServiceResult
                {
                    Summary = responseSummary
                });
            }

            return await GenerateSummaryInternalAsync(task, lang, cancellationToken);
        }

        public async Task<Result<AgileCoachSummaryServiceResult>> RegenerateSummaryAsync(Guid taskId, string lang, CancellationToken cancellationToken = default)
        {
            var taskResult = await GetAndValidateTaskOwnershipAsync(taskId, cancellationToken);
            if (!taskResult.IsSuccess) return Result.Failure<AgileCoachSummaryServiceResult>(taskResult.Error!);

            var task = taskResult.Value!;
            return await GenerateSummaryInternalAsync(task, lang, cancellationToken);
        }

        private async Task<Result<AgileCoachSummaryServiceResult>> GenerateSummaryInternalAsync(TaskItem task, string lang, CancellationToken cancellationToken)
        {
            try
            {
                var session = await _chatSessionRepository.GetQueryable()
                    .Include(s => s.Messages)
                    .FirstOrDefaultAsync(s => s.ProjectId == task.UserStory!.Project.Id, cancellationToken);
                var qaContext       = BuildQAPairsContext(session);
                var snapshotContext = BuildSnapshotContext(task.UserStory!.Project);
                var userStoryContext = BuildUserStoryContext(task.UserStory!, lang);

                var aiResult = await _agileCoachAgent.GenerateSummaryAsync(
                    lang == "ar" ? task.TitleAr : task.TitleEn,
                    (lang == "ar" ? task.DescriptionAr : task.DescriptionEn) ?? string.Empty,
                    task.UserStory!.Project.Id,
                    lang,
                    snapshotContext,
                    qaContext,
                    userStoryContext);

                if (aiResult.SummaryEn != null)
                {
                    task.TechnicalSummaryEn = aiResult.SummaryEn.Content;
                }
                
                if (aiResult.SummaryAr != null)
                {
                    task.TechnicalSummaryAr = aiResult.SummaryAr.Content;
                }

                // NOTE: We do not call SaveChangesAsync here. The Controller will do it.

                var content = lang == "ar" ? task.TechnicalSummaryAr : task.TechnicalSummaryEn;

                var responseSummary = new AgileCoachSummaryResponse
                {
                    Id = task.Id,
                    TaskItemId = task.Id,
                    Content = content ?? string.Empty,
                    GeneratedAt = DateTime.UtcNow,
                    IsNewlyGenerated = true
                };

                return Result.Success(new AgileCoachSummaryServiceResult
                {
                    Summary = responseSummary
                });
            }
            catch (AgileCoachException ex)
            {
                if (ex.Message.Contains("No relevant context found"))
                {
                    return Result.Failure<AgileCoachSummaryServiceResult>(AgileCoachErrors.KnowledgeBaseEmpty(ex.Message));
                }
                return Result.Failure<AgileCoachSummaryServiceResult>(AgileCoachErrors.SummaryGenerationFailed(ex.Message));
            }
            catch (Exception ex)
            {
                return Result.Failure<AgileCoachSummaryServiceResult>(AgileCoachErrors.SummaryGenerationFailed(ex.Message));
            }
        }

        public async IAsyncEnumerable<string> StreamChatAsync(Guid taskId, string userMessage, List<ChatMessageDto> history, string lang)
        {
            var taskResult = await GetAndValidateTaskOwnershipAsync(taskId, CancellationToken.None);
            if (!taskResult.IsSuccess)
            {
                // The controller SSE loop detects chunks starting with "__ERROR__:"
                // and writes them as a terminal error event: "event: error\ndata: {code}\n\n"
                yield return $"__ERROR__:{taskResult.Error!.Code}";
                yield break;
            }

            var task = taskResult.Value!;
            IAsyncEnumerable<string>? stream = null;
            string? error = null;
            try
            {
                var session = await _chatSessionRepository.GetQueryable()
                    .Include(s => s.Messages)
                    .FirstOrDefaultAsync(s => s.ProjectId == task.UserStory!.Project.Id, CancellationToken.None);
                var qaContext       = BuildQAPairsContext(session);
                var snapshotContext = BuildSnapshotContext(task.UserStory!.Project);
                var userStoryContext = BuildUserStoryContext(task.UserStory!, lang);

                stream = _agileCoachAgent.StreamChatAsync(
                    userMessage,
                    history,
                    task.UserStory!.Project.Id,
                    lang,
                    lang == "ar" ? task.TitleAr : task.TitleEn,
                    (lang == "ar" ? task.DescriptionAr : task.DescriptionEn) ?? string.Empty,
                    snapshotContext,
                    qaContext,
                    userStoryContext);
            }
            catch (Exception ex)
            {
                error = $"__ERROR__:{ex.Message}";
            }

            if (error != null)
            {
                yield return error;
                yield break;
            }

            IAsyncEnumerator<string>? enumerator = null;
            try
            {
                enumerator = stream!.GetAsyncEnumerator();
            }
            catch (Exception ex)
            {
                error = $"__ERROR__:{ex.Message}";
            }

            if (error != null)
            {
                yield return error;
                yield break;
            }
            
            while (true)
            {
                string? chunk = null;
                try
                {
                    if (!await enumerator!.MoveNextAsync())
                    {
                        break;
                    }
                    chunk = enumerator.Current;
                }
                catch (Exception ex)
                {
                    error = $"__ERROR__:{ex.Message}";
                    break;
                }
                
                if (error != null)
                {
                    yield return error;
                    break;
                }
                
                if (chunk != null)
                {
                    yield return chunk;
                }
            }
        }

        public async Task<Result<List<AgileCoachChatMessageDto>>> GetChatHistoryAsync(Guid taskId, string lang)
        {
            var messages = await _chatMessageRepository
                .GetQueryable()
                .Where(m => m.TaskId == taskId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new AgileCoachChatMessageDto
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    Lang = m.Lang,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return Result<List<AgileCoachChatMessageDto>>.Success(messages);
        }

        public async Task<Result> SaveChatMessageAsync(Guid taskId, string role, string content, string lang)
        {
            var message = new AgileCoachChatMessage
            {
                TaskId = taskId,
                Role = role,
                Content = content,
                Lang = lang
            };
            await _chatMessageRepository.AddAsync(message);
            return Result.Success();
        }

        private static string BuildQAPairsContext(ProjectChatSession? session)
        {
            if (session?.Messages == null || !session.Messages.Any())
                return string.Empty;

            var messages = session.Messages
                .OrderBy(m => m.SequenceIndex)
                .ToList();

            var pairs = new StringBuilder();
            for (int i = 0; i < messages.Count - 1; i++)
            {
                var current = messages[i];
                var next = messages[i + 1];

                if (current.Role == "Assistant" && next.Role == "User"
                    && next.Content?.Length >= 30)
                {
                    pairs.AppendLine($"Q: {current.Content.Trim()}");
                    pairs.AppendLine($"A: {next.Content.Trim()}");
                    pairs.AppendLine();
                    i++;
                }
            }
            return pairs.ToString();
        }

        private static string BuildSnapshotContext(Project project)
        {
            var snapshot = project.RequirementsSnapshot;
            if (snapshot == null) return string.Empty;

            var sb = new StringBuilder();

            if (snapshot.BusinessRequirements?.Any() == true)
            {
                sb.AppendLine("## Business Requirements");
                snapshot.BusinessRequirements.ForEach(r => sb.AppendLine($"- {r}"));
            }
            if (snapshot.TechnicalRequirements?.Any() == true)
            {
                sb.AppendLine("## Technical Requirements");
                snapshot.TechnicalRequirements.ForEach(r => sb.AppendLine($"- {r}"));
            }
            if (snapshot.Constraints?.Any() == true)
            {
                sb.AppendLine("## Constraints");
                snapshot.Constraints.ForEach(r => sb.AppendLine($"- {r}"));
            }
            if (snapshot.Integrations?.Any() == true)
            {
                sb.AppendLine("## Integrations");
                snapshot.Integrations.ForEach(r => sb.AppendLine($"- {r}"));
            }
            if (snapshot.ScaleRequirements?.Any() == true)
            {
                sb.AppendLine("## Scale Requirements");
                snapshot.ScaleRequirements.ForEach(r => sb.AppendLine($"- {r}"));
            }
            return sb.ToString();
        }

        private static string BuildUserStoryContext(UserStory userStory, string lang)
        {
            if (userStory == null) return string.Empty;

            var title = lang == "ar" ? userStory.TitleAr : userStory.TitleEn;
            var description = lang == "ar" ? userStory.DescriptionAr : userStory.DescriptionEn;
            var criteria = lang == "ar"
                ? userStory.AcceptanceCriteriaAr
                : userStory.AcceptanceCriteriaEn;

            var sb = new StringBuilder();
            sb.AppendLine($"User Story: {title}");
            if (!string.IsNullOrWhiteSpace(description))
                sb.AppendLine($"Description: {description}");
            if (!string.IsNullOrWhiteSpace(criteria))
                sb.AppendLine($"Acceptance Criteria: {criteria}");
            return sb.ToString();
        }
    }
}
