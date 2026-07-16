using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskPilot.Data.Repositories;
using TaskPilot.Data.Repositories.Interfaces;
using TaskPilot.DTOs.Tasks.Attachments;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;

namespace TaskPilot.Services.Implementations
{
    public class TaskAttachmentService : ITaskAttachmentService
    {
        private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
        private readonly IRepository<TaskAttachment> _attachmentRepository;
        private readonly IRepository<TaskItem> _taskRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IProjectEmployeeRepository _projectEmployeeRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<TaskAttachmentService> _logger;

        public TaskAttachmentService(
            IRepository<TaskAttachment> attachmentRepository,
            IRepository<TaskItem> taskRepository,
            IRepository<User> userRepository,
            IProjectEmployeeRepository projectEmployeeRepository,
            IFileStorageService fileStorageService,
            ILogger<TaskAttachmentService> logger)
        {
            _attachmentRepository = attachmentRepository;
            _taskRepository = taskRepository;
            _userRepository = userRepository;
            _projectEmployeeRepository = projectEmployeeRepository;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        public async Task<Result<TaskAttachmentDto>> UploadAttachmentAsync(
            Guid taskId,
            Guid userId,
            IFormFile file,
            CancellationToken ct = default)
        {
            if (file == null || file.Length == 0)
            {
                return Result.Failure<TaskAttachmentDto>(TaskAttachmentErrors.InvalidFile);
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return Result.Failure<TaskAttachmentDto>(TaskAttachmentErrors.FileTooLarge);
            }

            var task = await _taskRepository.GetByIdAsync(taskId, t => t.Sprint!, t => t.UserStory!);
            if (task == null)
            {
                return Result.Failure<TaskAttachmentDto>(TaskErrors.TaskNotFound);
            }

            var projectId = task.Sprint?.ProjectId ?? task.UserStory?.ProjectId ?? Guid.Empty;
            var isPm = await _projectEmployeeRepository.IsProjectManagerAsync(projectId, userId, ct);
            var isAssignee = task.EmployeeId == userId;

            if (!isPm && !isAssignee)
            {
                return Result.Failure<TaskAttachmentDto>(TaskErrors.ForbiddenTaskUpdate);
            }

            var uploadResult = await _fileStorageService.UploadFileAsync(file, $"task-attachments/{taskId}");
            if (uploadResult.IsFailure)
            {
                return Result.Failure<TaskAttachmentDto>(uploadResult.Error);
            }

            var attachment = new TaskAttachment
            {
                TaskId = taskId,
                FileUrl = uploadResult.Value.Url,
                PublicId = uploadResult.Value.PublicId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length
            };

            await _attachmentRepository.AddAsync(attachment);

            var user = await _userRepository.GetByIdAsync(userId);

            var dto = new TaskAttachmentDto
            {
                Id = attachment.Id,
                FileName = attachment.FileName,
                FileUrl = attachment.FileUrl,
                ContentType = attachment.ContentType,
                FileSize = attachment.FileSize,
                UploadedAt = attachment.CreatedAt,
                UploaderId = userId,
                UploaderNameEn = user != null ? $"{user.FirstNameEn} {user.LastNameEn}" : string.Empty,
                UploaderNameAr = user != null ? $"{user.FirstNameAr} {user.LastNameAr}" : string.Empty,
                UploaderRole = user switch
                {
                    Employee => "Employee",
                    ProjectManager => "ProjectManager",
                    _ => string.Empty
                }
            };

            return Result.Success(dto);
        }

        public async Task<Result> DeleteAttachmentAsync(
            Guid attachmentId,
            Guid userId,
            CancellationToken ct = default)
        {
            var attachment = await _attachmentRepository.GetByIdAsync(attachmentId, a => a.Task!, a => a.Task.Sprint!, a => a.Task.UserStory!);
            if (attachment == null)
            {
                return Result.Failure(TaskAttachmentErrors.AttachmentNotFound);
            }

            var isUploader = attachment.CreatedBy == userId;
            var projectId = attachment.Task.Sprint?.ProjectId ?? attachment.Task.UserStory?.ProjectId ?? Guid.Empty;
            var isPm = await _projectEmployeeRepository.IsProjectManagerAsync(projectId, userId, ct);

            if (!isUploader && !isPm)
            {
                return Result.Failure(TaskAttachmentErrors.AttachmentForbidden);
            }

            // Delete from Cloudinary
            if (!string.IsNullOrEmpty(attachment.PublicId))
            {
                var deleteStorageResult = await _fileStorageService.DeleteFileAsync(attachment.PublicId);
                if (deleteStorageResult.IsFailure)
                {
                    _logger.LogWarning("Failed to delete attachment from Cloudinary for PublicId {PublicId}", attachment.PublicId);
                }
            }

            _attachmentRepository.Delete(attachment);
            return Result.Success();
        }

        public async Task<Result<IEnumerable<TaskAttachmentDto>>> GetAttachmentsAsync(
            Guid taskId,
            Guid userId,
            CancellationToken ct = default)
        {
            var task = await _taskRepository.GetByIdAsync(taskId, t => t.Sprint!, t => t.UserStory!);
            if (task == null)
            {
                return Result.Failure<IEnumerable<TaskAttachmentDto>>(TaskErrors.TaskNotFound);
            }

            var projectId = task.Sprint?.ProjectId ?? task.UserStory?.ProjectId ?? Guid.Empty;
            var isPm = await _projectEmployeeRepository.IsProjectManagerAsync(projectId, userId, ct);
            var isAssignee = task.EmployeeId == userId;

            if (!isPm && !isAssignee)
            {
                return Result.Failure<IEnumerable<TaskAttachmentDto>>(TaskErrors.ForbiddenTaskUpdate);
            }

            var attachments = await _attachmentRepository.GetQueryable()
                .Where(a => a.TaskId == taskId)
                .OrderBy(a => a.CreatedAt)
                .ToListAsync(ct);

            // Fetch uploader user details
            var uploaderIds = attachments.Select(a => a.CreatedBy).Distinct().Where(id => id.HasValue).Select(id => id!.Value).ToList();
            var uploaders = await _userRepository.GetQueryable()
                .Where(u => uploaderIds.Contains(u.Id))
                .ToListAsync(ct);

            var dtos = attachments.Select(a =>
            {
                var user = uploaders.FirstOrDefault(u => u.Id == a.CreatedBy);
                return new TaskAttachmentDto
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    FileUrl = a.FileUrl,
                    ContentType = a.ContentType,
                    FileSize = a.FileSize,
                    UploadedAt = a.CreatedAt,
                    UploaderId = a.CreatedBy,
                    UploaderNameEn = user != null ? $"{user.FirstNameEn} {user.LastNameEn}" : string.Empty,
                    UploaderNameAr = user != null ? $"{user.FirstNameAr} {user.LastNameAr}" : string.Empty,
                    UploaderRole = user switch
                    {
                        Employee => "Employee",
                        ProjectManager => "ProjectManager",
                        _ => string.Empty
                    }
                };
            });

            return Result.Success(dtos);
        }
    }
}
