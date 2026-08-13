using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using TaskPilot.Data.Context;
using TaskPilot.DTOs.Projects;
using TaskPilot.Models.Common.Errors;
using TaskPilot.Models.Common.Results;
using TaskPilot.Models.Entities;
using TaskPilot.Models.Enums;
using TaskPilot.Services.BackgroundJobs;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    public sealed class ProjectSetupService : IProjectSetupService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly ITechStackService _techStackService;
        private readonly IBackgroundJobClient _jobs;

        public ProjectSetupService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            ITechStackService techStackService,
            IBackgroundJobClient jobs)
        {
            _context = context;
            _currentUser = currentUser;
            _techStackService = techStackService;
            _jobs = jobs;
        }

        public async Task<Result<ProjectSetupDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAsync(projectId, cancellationToken);
            if (loaded.Error != null) return Result.Failure<ProjectSetupDto>(loaded.Error);
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(ToDto(loaded.Project!, loaded.State!));
        }

        public async Task<Result<ProjectSetupDto>> GenerateTechStackSuggestionAsync(
            Guid projectId,
            bool regenerate,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAsync(projectId, cancellationToken);
            if (loaded.Error != null) return Result.Failure<ProjectSetupDto>(loaded.Error);
            var project = loaded.Project!;
            var state = loaded.State!;

            if (!regenerate && !string.IsNullOrWhiteSpace(state.TechStackSuggestionJson))
                return Result.Success(ToDto(project, state));

            if (state.WbsStatus is BackgroundSetupStatus.Queued or BackgroundSetupStatus.Running or BackgroundSetupStatus.Succeeded)
                return Result.Failure<ProjectSetupDto>(CommonErrors.Conflict("TECH_STACK_LOCKED", "The tech stack cannot be changed after WBS generation starts."));

            try
            {
                var suggestion = await _techStackService.SuggestAsync(projectId, cancellationToken);
                if (suggestion.IsFailure)
                {
                    state.TechStackStatus = TechStackSetupStatus.Failed;
                    state.TechStackError = suggestion.Error.Description;
                    await _context.SaveChangesAsync(cancellationToken);
                    return Result.Failure<ProjectSetupDto>(suggestion.Errors);
                }

                state.TechStackSuggestionJson = JsonSerializer.Serialize(suggestion.Value);
                state.TechStackStatus = TechStackSetupStatus.Suggested;
                state.TechStackError = null;
                await _context.SaveChangesAsync(cancellationToken);
                return Result.Success(ToDto(project, state));
            }
            catch (Exception ex)
            {
                state.TechStackStatus = TechStackSetupStatus.Failed;
                state.TechStackError = ex.Message;
                await _context.SaveChangesAsync(cancellationToken);
                return Result.Failure<ProjectSetupDto>(CommonErrors.OperationFailed("Tech stack suggestion failed. Please retry."));
            }
        }

        public async Task<Result<ProjectSetupDto>> ConfirmTechStackAsync(
            Guid projectId,
            ConfirmTechStackRequest request,
            CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAsync(projectId, cancellationToken);
            if (loaded.Error != null) return Result.Failure<ProjectSetupDto>(loaded.Error);
            var project = loaded.Project!;
            var state = loaded.State!;

            if (request.TechStack.Count == 0 || request.PlatformTargets.Count == 0 || string.IsNullOrWhiteSpace(request.ProjectType))
                return Result.Failure<ProjectSetupDto>(CommonErrors.InvalidInput("Choose at least one technology, one platform, and a project type."));

            if (state.WbsStatus is BackgroundSetupStatus.Queued or BackgroundSetupStatus.Running or BackgroundSetupStatus.Succeeded)
                return Result.Failure<ProjectSetupDto>(CommonErrors.Conflict("TECH_STACK_LOCKED", "The tech stack cannot be changed after WBS generation starts."));

            project.TechStack = request.TechStack.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            project.PlatformTargets = request.PlatformTargets.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            project.ProjectType = request.ProjectType.Trim();
            state.TechStackStatus = TechStackSetupStatus.Confirmed;
            state.TechStackError = null;
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(ToDto(project, state));
        }

        public async Task<Result<ProjectSetupDto>> QueueWbsAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAsync(projectId, cancellationToken);
            if (loaded.Error != null) return Result.Failure<ProjectSetupDto>(loaded.Error);
            var project = loaded.Project!;
            var state = loaded.State!;

            if (state.TechStackStatus != TechStackSetupStatus.Confirmed)
                return Result.Failure<ProjectSetupDto>(CommonErrors.Conflict("TECH_STACK_NOT_CONFIRMED", "Confirm the tech stack before generating the WBS."));

            if (state.WbsStatus is BackgroundSetupStatus.Queued or BackgroundSetupStatus.Running or BackgroundSetupStatus.Succeeded)
                return Result.Success(ToDto(project, state));

            if (await _context.UserStories.AnyAsync(x => x.ProjectId == projectId && !x.IsDeleted, cancellationToken))
            {
                state.WbsStatus = BackgroundSetupStatus.Succeeded;
                state.WbsCompletedAt ??= DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                return Result.Success(ToDto(project, state));
            }

            state.WbsStatus = BackgroundSetupStatus.Queued;
            state.WbsError = null;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                return await GetAsync(projectId, cancellationToken);
            }
            state.WbsJobId = _jobs.Enqueue<WbsGenerationJob>(job => job.ExecuteAsync(projectId, null!));
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                return await GetAsync(projectId, cancellationToken);
            }
            return Result.Success(ToDto(project, state));
        }

        public async Task<Result<ProjectSetupDto>> QueueSkillsAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            var loaded = await LoadOwnedAsync(projectId, cancellationToken);
            if (loaded.Error != null) return Result.Failure<ProjectSetupDto>(loaded.Error);
            var project = loaded.Project!;
            var state = loaded.State!;

            if (state.WbsStatus != BackgroundSetupStatus.Succeeded)
                return Result.Failure<ProjectSetupDto>(CommonErrors.Conflict("WBS_NOT_READY", "Generate the WBS before enriching task skills."));

            if (state.SkillsStatus is BackgroundSetupStatus.Queued or BackgroundSetupStatus.Running or BackgroundSetupStatus.Succeeded)
                return Result.Success(ToDto(project, state));

            state.SkillsStatus = BackgroundSetupStatus.Queued;
            state.SkillsError = null;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                return await GetAsync(projectId, cancellationToken);
            }
            state.SkillsJobId = _jobs.Enqueue<WbsSkillEnrichmentJob>(job => job.ExecuteAsync(projectId, null!));
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _context.ChangeTracker.Clear();
                return await GetAsync(projectId, cancellationToken);
            }
            return Result.Success(ToDto(project, state));
        }

        private async Task<(Project? Project, ProjectSetupState? State, TaskPilot.Models.Common.Errors.Error? Error)> LoadOwnedAsync(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var userId = _currentUser.UserId;
            if (!userId.HasValue) return (null, null, CommonErrors.Unauthorized());

            var project = await _context.Projects
                .Include(x => x.SetupState)
                .FirstOrDefaultAsync(x => x.Id == projectId && !x.IsDeleted, cancellationToken);
            if (project == null) return (null, null, CommonErrors.NotFound("Project"));
            if (project.ManagerId != userId.Value) return (null, null, CommonErrors.Forbidden("Only the project manager can manage project setup."));

            var state = project.SetupState;
            if (state == null)
            {
                var hasWbs = await _context.UserStories.AnyAsync(x => x.ProjectId == projectId && !x.IsDeleted, cancellationToken);
                state = new ProjectSetupState
                {
                    ProjectId = projectId,
                    TechStackStatus = project.TechStack.Count > 0 ? TechStackSetupStatus.Confirmed : TechStackSetupStatus.NotStarted,
                    WbsStatus = hasWbs ? BackgroundSetupStatus.Succeeded : BackgroundSetupStatus.NotStarted,
                    SkillsStatus = hasWbs ? BackgroundSetupStatus.Succeeded : BackgroundSetupStatus.NotStarted,
                    WbsCompletedAt = hasWbs ? DateTime.UtcNow : null,
                    SkillsCompletedAt = hasWbs ? DateTime.UtcNow : null
                };
                project.SetupState = state;
                _context.ProjectSetupStates.Add(state);
            }

            return (project, state, null);
        }

        public static ProjectSetupDto ToDto(Project project, ProjectSetupState state)
        {
            JsonElement? suggestion = null;
            if (!string.IsNullOrWhiteSpace(state.TechStackSuggestionJson))
            {
                // JsonElement preserves the original property names verbatim. Older
                // rows were stored with C# PascalCase, so normalize the typed value
                // before exposing it through the camelCase HTTP contract.
                var typedSuggestion = JsonSerializer.Deserialize<TaskPilot.AI.Models.Planning.TechStackSuggestion>(
                    state.TechStackSuggestionJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (typedSuggestion != null)
                    suggestion = JsonSerializer.SerializeToElement(typedSuggestion, JsonSerializerOptions.Web);
            }

            return new ProjectSetupDto
            {
                ProjectId = project.Id,
                ProjectName = project.NameEn,
                OverallStatus = GetOverallStatus(state),
                TechStack = new TechStackSetupDto
                {
                    Status = state.TechStackStatus,
                    Suggestion = suggestion,
                    ConfirmedStack = project.TechStack,
                    Platforms = project.PlatformTargets,
                    ProjectType = project.ProjectType,
                    Error = state.TechStackError
                },
                Wbs = new SetupJobDto
                {
                    Status = state.WbsStatus,
                    JobId = state.WbsJobId,
                    AttemptCount = state.WbsAttemptCount,
                    ItemsCreated = state.UserStoriesCreated,
                    SecondaryItemsCreated = state.TasksCreated,
                    StartedAt = state.WbsStartedAt,
                    CompletedAt = state.WbsCompletedAt,
                    Error = state.WbsError
                },
                Skills = new SetupJobDto
                {
                    Status = state.SkillsStatus,
                    JobId = state.SkillsJobId,
                    AttemptCount = state.SkillsAttemptCount,
                    ItemsCreated = state.TasksEnriched,
                    SecondaryItemsCreated = state.SkillsCreated,
                    ItemsSkipped = state.TasksSkipped,
                    StartedAt = state.SkillsStartedAt,
                    CompletedAt = state.SkillsCompletedAt,
                    Error = state.SkillsError
                }
            };
        }

        public static ProjectSetupOverallStatus GetOverallStatus(ProjectSetupState state)
        {
            if (state.TechStackStatus == TechStackSetupStatus.Failed || state.WbsStatus == BackgroundSetupStatus.Failed)
                return ProjectSetupOverallStatus.Failed;
            if (state.TechStackStatus != TechStackSetupStatus.Confirmed)
                return ProjectSetupOverallStatus.NeedsTechStack;
            if (state.WbsStatus == BackgroundSetupStatus.NotStarted)
                return ProjectSetupOverallStatus.ReadyForWbs;
            if (state.WbsStatus == BackgroundSetupStatus.Queued)
                return ProjectSetupOverallStatus.WbsQueued;
            if (state.WbsStatus == BackgroundSetupStatus.Running)
                return ProjectSetupOverallStatus.WbsGenerating;
            if (state.WbsStatus == BackgroundSetupStatus.Succeeded && state.SkillsStatus == BackgroundSetupStatus.NotStarted)
                return ProjectSetupOverallStatus.WbsReady;
            if (state.SkillsStatus is BackgroundSetupStatus.Queued or BackgroundSetupStatus.Running)
                return ProjectSetupOverallStatus.EnrichingSkills;
            if (state.SkillsStatus == BackgroundSetupStatus.Succeeded)
                return ProjectSetupOverallStatus.Ready;
            return ProjectSetupOverallStatus.ReadyWithWarnings;
        }
    }
}
