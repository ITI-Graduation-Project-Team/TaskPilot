using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Tasks.Comments;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services.Implementations
{
    public class TaskCommentService : ITaskCommentService
    {
        private readonly IRepository<TaskComment> _commentRepository;
        private readonly IRepository<TaskItem> _taskRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IProjectEmployeeRepository _projectEmployeeRepository;
        private readonly ILogger<TaskCommentService> _logger;

        public TaskCommentService(
            IRepository<TaskComment> commentRepository,
            IRepository<TaskItem> taskRepository,
            IRepository<User> userRepository,
            IProjectEmployeeRepository projectEmployeeRepository,
            ILogger<TaskCommentService> logger)
        {
            _commentRepository = commentRepository;
            _taskRepository = taskRepository;
            _userRepository = userRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _logger = logger;
        }

        public async Task<Result<TaskCommentDto>> AddCommentAsync(
            Guid taskId,
            Guid userId,
            AddTaskCommentRequest request,
            CancellationToken ct = default)
        {
            var task = await _taskRepository.GetByIdAsync(taskId, t => t.Sprint!, t => t.UserStory!);
            if (task == null)
            {
                return Result.Failure<TaskCommentDto>(TaskErrors.TaskNotFound);
            }

            var projectId = task.Sprint?.ProjectId ?? task.UserStory?.ProjectId ?? Guid.Empty;
            var isPm = await _projectEmployeeRepository.IsProjectManagerAsync(projectId, userId, ct);
            var isAssignee = task.EmployeeId == userId;

            if (!isPm && !isAssignee)
            {
                return Result.Failure<TaskCommentDto>(TaskErrors.ForbiddenTaskUpdate);
            }

            var comment = new TaskComment
            {
                TaskId = taskId,
                UserId = userId,
                Content = request.Content
            };

            await _commentRepository.AddAsync(comment);

            // Fetch the user to return author details
            var user = await _userRepository.GetByIdAsync(userId);

            var dto = new TaskCommentDto
            {
                Id = comment.Id,
                Content = comment.Content,
                AuthorId = userId,
                AuthorNameEn = user != null ? $"{user.FirstNameEn} {user.LastNameEn}" : string.Empty,
                AuthorNameAr = user != null ? $"{user.FirstNameAr} {user.LastNameAr}" : string.Empty,
                AuthorRole = user switch
                {
                    Employee => "Employee",
                    ProjectManager => "ProjectManager",
                    _ => string.Empty
                },
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.ModifiedAt
            };

            return Result.Success(dto);
        }

        public async Task<Result<TaskCommentDto>> UpdateCommentAsync(
            Guid commentId,
            Guid userId,
            UpdateTaskCommentRequest request,
            CancellationToken ct = default)
        {
            var comment = await _commentRepository.GetByIdAsync(commentId, c => c.User!);
            if (comment == null)
            {
                return Result.Failure<TaskCommentDto>(TaskCommentErrors.CommentNotFound);
            }

            if (comment.UserId != userId)
            {
                return Result.Failure<TaskCommentDto>(TaskCommentErrors.CommentForbidden);
            }

            comment.Content = request.Content;
            _commentRepository.Update(comment);

            var dto = new TaskCommentDto
            {
                Id = comment.Id,
                Content = comment.Content,
                AuthorId = comment.UserId ?? Guid.Empty,
                AuthorNameEn = comment.User != null ? $"{comment.User.FirstNameEn} {comment.User.LastNameEn}" : string.Empty,
                AuthorNameAr = comment.User != null ? $"{comment.User.FirstNameAr} {comment.User.LastNameAr}" : string.Empty,
                AuthorRole = comment.User switch
                {
                    Employee => "Employee",
                    ProjectManager => "ProjectManager",
                    _ => string.Empty
                },
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.ModifiedAt
            };

            return Result.Success(dto);
        }

        public async Task<Result> DeleteCommentAsync(
            Guid commentId,
            Guid userId,
            CancellationToken ct = default)
        {
            var comment = await _commentRepository.GetByIdAsync(commentId, c => c.Task!, c => c.Task.Sprint!, c => c.Task.UserStory!);
            if (comment == null)
            {
                return Result.Failure(TaskCommentErrors.CommentNotFound);
            }

            var isAuthor = comment.UserId == userId;
            var projectId = comment.Task.Sprint?.ProjectId ?? comment.Task.UserStory?.ProjectId ?? Guid.Empty;
            var isPm = await _projectEmployeeRepository.IsProjectManagerAsync(projectId, userId, ct);

            if (!isAuthor && !isPm)
            {
                return Result.Failure(TaskCommentErrors.CommentForbidden);
            }

            _commentRepository.Delete(comment);
            return Result.Success();
        }

        public async Task<Result<IEnumerable<TaskCommentDto>>> GetCommentsAsync(
            Guid taskId,
            Guid userId,
            CancellationToken ct = default)
        {
            var task = await _taskRepository.GetByIdAsync(taskId, t => t.Sprint!, t => t.UserStory!);
            if (task == null)
            {
                return Result.Failure<IEnumerable<TaskCommentDto>>(TaskErrors.TaskNotFound);
            }

            var projectId = task.Sprint?.ProjectId ?? task.UserStory?.ProjectId ?? Guid.Empty;
            var isPm = await _projectEmployeeRepository.IsProjectManagerAsync(projectId, userId, ct);
            var isAssignee = task.EmployeeId == userId;

            if (!isPm && !isAssignee)
            {
                return Result.Failure<IEnumerable<TaskCommentDto>>(TaskErrors.ForbiddenTaskUpdate);
            }

            var comments = await _commentRepository.GetQueryable()
                .Where(c => c.TaskId == taskId)
                .Include(c => c.User)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(ct);

            var dtos = comments.Select(c => new TaskCommentDto
            {
                Id = c.Id,
                Content = c.Content,
                AuthorId = c.UserId ?? Guid.Empty,
                AuthorNameEn = c.User != null ? $"{c.User.FirstNameEn} {c.User.LastNameEn}" : string.Empty,
                AuthorNameAr = c.User != null ? $"{c.User.FirstNameAr} {c.User.LastNameAr}" : string.Empty,
                AuthorRole = c.User switch
                {
                    Employee => "Employee",
                    ProjectManager => "ProjectManager",
                    _ => string.Empty
                },
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.ModifiedAt
            });

            return Result.Success(dtos);
        }
    }
}
