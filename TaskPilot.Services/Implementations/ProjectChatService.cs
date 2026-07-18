using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Chat;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.AI.Services.Interfaces;
using TaskPilot.Data.Repositories;

namespace TaskPilot.Services.Implementations
{
    public class ProjectChatService : IProjectChatService, IAiProjectChatService
    {
        private readonly IProjectChatSessionRepository _chatSessionRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProjectChatService> _logger;

        public ProjectChatService(
            IProjectChatSessionRepository chatSessionRepository,
            IRepository<Project> projectRepository,
            IUnitOfWork unitOfWork,
            ILogger<ProjectChatService> logger)
        {
            _chatSessionRepository = chatSessionRepository;
            _projectRepository = projectRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<ProjectChatSessionDto>> GetOrCreateSessionAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var session = await _chatSessionRepository.GetByProjectIdWithMessagesAsync(projectId, cancellationToken);
            if (session == null)
            {
                var project = await _projectRepository.GetByIdAsync(projectId);
                if (project == null)
                    return Result.Failure<ProjectChatSessionDto>(CommonErrors.NotFound("Project"));

                session = new ProjectChatSession { ProjectId = projectId };
                await _chatSessionRepository.AddAsync(session);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success(MapToDto(session));
        }

        public async Task<Result<ProjectChatSessionDto>> GetSessionAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var session = await _chatSessionRepository.GetByProjectIdWithMessagesAsync(projectId, cancellationToken);
            if (session == null)
                return Result.Failure<ProjectChatSessionDto>(CommonErrors.NotFound("ProjectChatSession"));

            return Result.Success(MapToDto(session));
        }

        public async Task<Result> AppendUserMessageAsync(Guid projectId, string content, CancellationToken cancellationToken = default)
        {
            return await AppendMessageAsync(projectId, "User", content, cancellationToken);
        }

        public async Task<Result> AppendAssistantMessageAsync(Guid projectId, string content, CancellationToken cancellationToken = default)
        {
            return await AppendMessageAsync(projectId, "Assistant", content, cancellationToken);
        }

        private async Task<Result> AppendMessageAsync(Guid projectId, string role, string content, CancellationToken cancellationToken)
        {
            return await AppendMessagesAsync(projectId, new List<(string, string)> { (role, content) }, cancellationToken);
        }

        public async Task<Result> AppendMessagesAsync(Guid projectId, List<(string Role, string Content)> messages, CancellationToken cancellationToken = default)
        {
            try
            {
                foreach (var (role, _) in messages)
                {
                    if (role != "User" && role != "Assistant")
                    {
                        return Result.Failure(new Error("InvalidRole", ErrorType.Validation, $"Invalid role value: {role}"));
                    }
                }

                var sessionResult = await GetOrCreateSessionAsync(projectId, cancellationToken);
                if (!sessionResult.IsSuccess)
                    return Result.Failure(sessionResult.Error);

                var session = await _chatSessionRepository.GetByProjectIdAsync(projectId, cancellationToken);
                if (session == null)
                    return Result.Failure(CommonErrors.NotFound("ProjectChatSession"));

                foreach (var (role, content) in messages)
                {
                    var message = new ProjectChatMessage
                    {
                        SessionId = session.Id,
                        Role = role,
                        Content = content,
                        SequenceIndex = session.Messages.Count + 1,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    session.Messages.Add(message);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist chat messages for project {ProjectId}.", projectId);
                return Result.Failure(new Error("PersistenceError", ErrorType.Failure, "Failed to persist chat messages."));
            }
        }

        private static ProjectChatSessionDto MapToDto(ProjectChatSession session)
        {
            return new ProjectChatSessionDto
            {
                Id = session.Id,
                ProjectId = session.ProjectId,
                BrdExtractedText = session.BrdExtractedText,
                Messages = session.Messages.Select(m => new ProjectChatMessageDto
                {
                    Id = m.Id,
                    SessionId = m.SessionId,
                    Role = m.Role,
                    Content = m.Content,
                    SequenceIndex = m.SequenceIndex,
                    Timestamp = m.Timestamp
                }).ToList()
            };
        }
    }
}
