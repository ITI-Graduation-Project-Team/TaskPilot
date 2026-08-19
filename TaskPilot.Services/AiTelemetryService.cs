using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaskPilot.Data.Context;
using TaskPilot.DTOs.Telemetry;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Services.Interfaces;
using TaskPilot.AI.Models.Telemetry;

namespace TaskPilot.Services
{
    public class AiTelemetryService : IAiTelemetryService
    {
        private readonly ApplicationDbContext _context;

        public AiTelemetryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogTelemetryBatchAsync(
            IReadOnlyCollection<AiUsageRecord> records,
            CancellationToken cancellationToken = default)
        {
            if (records.Count == 0)
                return;

            var timestamp = DateTime.UtcNow;
            var logs = records.Select(record => new AiTelemetryLog
            {
                UserId = record.UserId,
                ProjectId = record.ProjectId,
                OperationType = record.OperationType,
                ModelName = record.ModelName,
                PromptTokens = record.PromptTokens,
                CachedPromptTokens = record.CachedPromptTokens,
                CompletionTokens = record.CompletionTokens,
                TotalTokens = record.PromptTokens + record.CompletionTokens,
                EstimatedCostUsd = record.EstimatedCostUsd,
                ResponseTimeMs = record.ResponseTimeMs,
                Status = record.Status,
                CalculationStatus = record.CalculationStatus,
                ErrorMessage = record.ErrorMessage,
                Timestamp = timestamp
            });

            _context.AiTelemetryLogs.AddRange(logs);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<Result<EmployeeAiSummaryDto>> GetEmployeeSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var logs = await _context.AiTelemetryLogs
                .Where(l => l.UserId == userId && l.CalculationStatus == "Calculated")
                .ToListAsync(cancellationToken);

            if (!logs.Any())
            {
                return Result<EmployeeAiSummaryDto>.Success(new EmployeeAiSummaryDto());
            }

            var summary = new EmployeeAiSummaryDto
            {
                TotalOperations = logs.Count,
                TotalTokens = logs.Sum(l => l.TotalTokens),
                TotalCostUsd = logs.Sum(l => l.EstimatedCostUsd),
                AverageResponseTimeMs = (long)logs.Average(l => l.ResponseTimeMs)
            };

            return Result<EmployeeAiSummaryDto>.Success(summary);
        }

        public async Task<Result<PagedResult<AiTelemetryLogDto>>> GetEmployeeLogsAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.AiTelemetryLogs
                .Include(l => l.User)
                .Include(l => l.Project)
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Timestamp);

            return await GetPagedLogsAsync(query, page, pageSize, cancellationToken);
        }

        public async Task<Result<ProjectAiSummaryDto>> GetProjectSummaryAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var project = await _context.Projects.FindAsync([projectId], cancellationToken);
            if (project == null)
            {
                return Result<ProjectAiSummaryDto>.Failure(new Models.Common.Errors.Error("PROJECT_NOT_FOUND", Models.Common.Errors.ErrorType.NotFound, "Project not found"));
            }

            var logs = await _context.AiTelemetryLogs
                .Where(l => l.ProjectId == projectId && l.CalculationStatus == "Calculated")
                .ToListAsync(cancellationToken);

            if (!logs.Any())
            {
                return Result<ProjectAiSummaryDto>.Success(new ProjectAiSummaryDto
                {
                    ProjectId = projectId,
                    ProjectName = project.NameEn
                });
            }

            var modelUsage = logs.GroupBy(l => l.ModelName)
                .ToDictionary(g => g.Key, g => g.Count());

            var summary = new ProjectAiSummaryDto
            {
                ProjectId = projectId,
                ProjectName = project.NameEn,
                TotalOperations = logs.Count,
                TotalTokens = logs.Sum(l => l.TotalTokens),
                TotalCostUsd = logs.Sum(l => l.EstimatedCostUsd),
                AverageResponseTimeMs = (long)logs.Average(l => l.ResponseTimeMs),
                ModelUsageCounts = modelUsage
            };

            return Result<ProjectAiSummaryDto>.Success(summary);
        }

        public async Task<Result<ManagedProjectsAiSummaryDto>> GetManagedProjectsSummaryAsync(
            Guid managerId,
            CancellationToken cancellationToken = default)
        {
            var logs = _context.AiTelemetryLogs
                .Where(log =>
                    log.CalculationStatus == "Calculated" &&
                    log.ProjectId.HasValue &&
                    _context.Projects.Any(project =>
                        project.Id == log.ProjectId.Value &&
                        project.ManagerId == managerId &&
                        !project.IsDeleted));

            var totalOperations = await logs.CountAsync(cancellationToken);
            if (totalOperations == 0)
            {
                return Result<ManagedProjectsAiSummaryDto>.Success(new ManagedProjectsAiSummaryDto());
            }

            var summary = new ManagedProjectsAiSummaryDto
            {
                TotalOperations = totalOperations,
                TotalTokens = await logs.SumAsync(log => log.TotalTokens, cancellationToken),
                TotalCostUsd = await logs.SumAsync(log => log.EstimatedCostUsd, cancellationToken),
                AverageResponseTimeMs = (long)await logs.AverageAsync(log => log.ResponseTimeMs, cancellationToken)
            };

            return Result<ManagedProjectsAiSummaryDto>.Success(summary);
        }

        public async Task<Result<List<ProjectMemberAiUsageDto>>> GetProjectMemberBreakdownAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var logs = await _context.AiTelemetryLogs
                .Include(l => l.User)
                .Where(l => l.ProjectId == projectId && l.CalculationStatus == "Calculated")
                .ToListAsync(cancellationToken);

            var breakdown = logs.GroupBy(l => l.UserId)
                .Select(g => {
                    var firstLog = g.First();
                    var userType = firstLog.User is ProjectManager ? "Project Manager" : "Employee";
                    return new ProjectMemberAiUsageDto
                    {
                        UserId = g.Key,
                        Email = firstLog.User.Email ?? string.Empty,
                        FullName = $"{firstLog.User.FirstNameEn} {firstLog.User.LastNameEn}",
                        Role = userType,
                        TotalOperations = g.Count(),
                        TotalTokens = g.Sum(x => x.TotalTokens),
                        TotalCostUsd = g.Sum(x => x.EstimatedCostUsd)
                    };
                })
                .ToList();

            return Result<List<ProjectMemberAiUsageDto>>.Success(breakdown);
        }

        public async Task<Result<PagedResult<AiTelemetryLogDto>>> GetProjectLogsAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.AiTelemetryLogs
                .Include(l => l.User)
                .Include(l => l.Project)
                .Where(l => l.ProjectId == projectId)
                .OrderByDescending(l => l.Timestamp);

            return await GetPagedLogsAsync(query, page, pageSize, cancellationToken);
        }

        public async Task<Result<AdminAiDashboardDto>> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
        {
            var logs = await _context.AiTelemetryLogs
                .Where(l => l.CalculationStatus == "Calculated")
                .ToListAsync(cancellationToken);

            if (!logs.Any())
            {
                return Result<AdminAiDashboardDto>.Success(new AdminAiDashboardDto());
            }

            var successfulOperations = logs.Count(l => l.Status == "Success");
            double successRate = logs.Count > 0 ? (double)successfulOperations / logs.Count * 100 : 0;

            var modelUsage = logs.GroupBy(l => l.ModelName)
                .ToDictionary(g => g.Key, g => g.Count());

            var operationUsage = logs.GroupBy(l => l.OperationType)
                .ToDictionary(g => g.Key, g => g.Count());

            var dashboard = new AdminAiDashboardDto
            {
                TotalOperations = logs.Count,
                TotalTokens = logs.Sum(l => l.TotalTokens),
                TotalCostUsd = logs.Sum(l => l.EstimatedCostUsd),
                AverageResponseTimeMs = (long)logs.Average(l => l.ResponseTimeMs),
                SuccessRate = successRate,
                ModelUsageCounts = modelUsage,
                OperationTypeCounts = operationUsage
            };

            return Result<AdminAiDashboardDto>.Success(dashboard);
        }

        public async Task<Result<PagedResult<AiTelemetryLogDto>>> GetAdminLogsAsync(
            Guid? userId,
            string? operationType,
            string? status,
            string? modelName,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.AiTelemetryLogs
                .Include(l => l.User)
                .Include(l => l.Project)
                .AsQueryable();

            if (userId.HasValue)
            {
                query = query.Where(l => l.UserId == userId.Value);
            }

            if (!string.IsNullOrWhiteSpace(operationType))
            {
                query = query.Where(l => l.OperationType == operationType);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                query = query.Where(l => l.ModelName == modelName);
            }

            var orderedQuery = query.OrderByDescending(l => l.Timestamp);

            return await GetPagedLogsAsync(orderedQuery, page, pageSize, cancellationToken);
        }

        private async Task<PagedResult<AiTelemetryLogDto>> GetPagedLogsAsync(
            IQueryable<AiTelemetryLog> query,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            int totalItems = await query.CountAsync(cancellationToken);
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new AiTelemetryLogDto
                {
                    Id = l.Id,
                    UserId = l.UserId,
                    UserEmail = l.User.Email ?? string.Empty,
                    UserFullName = $"{l.User.FirstNameEn} {l.User.LastNameEn}",
                    ProjectId = l.ProjectId,
                    ProjectName = l.Project != null ? l.Project.NameEn : null,
                    OperationType = l.OperationType,
                    ModelName = l.ModelName,
                    PromptTokens = l.PromptTokens,
                    CompletionTokens = l.CompletionTokens,
                    TotalTokens = l.TotalTokens,
                    EstimatedCostUsd = l.EstimatedCostUsd,
                    ResponseTimeMs = l.ResponseTimeMs,
                    Status = l.Status,
                    ErrorMessage = l.ErrorMessage,
                    Timestamp = l.Timestamp
                })
                .ToListAsync(cancellationToken);

            return new PagedResult<AiTelemetryLogDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
                HasPreviousPage = page > 1,
                HasNextPage = page < totalPages
            };
        }

    }
}
