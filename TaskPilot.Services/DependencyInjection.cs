using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TaskPilot.Models.Common;
using TaskPilot.Services.Filters;
using TaskPilot.Services.Implementations;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;
using TaskPilot.Services.Repositories;

namespace TaskPilot.Services
{
    public static class DependencyInjection
    {
        // لاحظ إضافة IConfiguration configuration هنا
        public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
        {
            // ── Business Services ──
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITaskCommentService, TaskPilot.Services.Implementations.TaskCommentService>();
            services.AddScoped<ITaskAttachmentService, TaskPilot.Services.Implementations.TaskAttachmentService>();

            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
            services.AddScoped<IUserSubscriptionService, UserSubscriptionService>();
            services.AddScoped<ILocalizationService, LocalizationService>();
            services.AddScoped<IRequirementFinalizationService, RequirementFinalizationService>();
            services.AddScoped<ICompanyPolicyService, CompanyPolicyService>();
            services.AddScoped<IProjectPolicyService, ProjectPolicyService>();
            services.AddScoped<IWbsSkillEnrichmentService, WbsSkillEnrichmentService>();
            services.AddScoped<ICalenderService, CalenderService>();
            services.AddScoped<IWbsPersistenceService, WbsPersistenceService>();
            services.AddScoped<IBacklogService, BacklogService>();
            services.AddScoped<IBacklogRegenerationService, BacklogRegenerationService>();
            services.AddScoped<ISprintPlanningService, SprintPlanningService>();
            services.AddScoped<ICapacityCalculationService, TaskPilot.Services.Implementations.CapacityCalculationService>();
            services.AddScoped<ISprintConfirmationService, SprintConfirmationService>();
            services.AddScoped<ISprintLifecycleService, SprintLifecycleService>();
            services.AddScoped<ITechStackService, TechStackService>();
            services.AddScoped<IWbsGenerationService, WbsGenerationService>();
            services.AddSingleton<ITemporaryBrdStore, TaskPilot.Services.Implementations.InMemoryTemporaryBrdStore>();
            services.AddScoped<INotificationService, TaskPilot.Services.Implementations.NotificationService>();
            services.AddScoped<IProjectChatService, TaskPilot.Services.Implementations.ProjectChatService>();
            services.AddScoped<IAiProjectsService, TaskPilot.Services.Implementations.AiProjectsService>();
            services.AddScoped<IAiTelemetryService, AiTelemetryService>();
            services.AddScoped<IFunctionInvocationFilter, AiTelemetryFilter>();
            services.AddScoped<TaskPilot.AI.Services.Interfaces.IAiProjectChatService, TaskPilot.Services.Implementations.ProjectChatService>();
            services.AddScoped<IAgileCoachService, TaskPilot.Services.Implementations.AgileCoachService>();
            services.AddScoped<TaskPilot.AI.Services.Interfaces.IAiBacklogService, TaskPilot.Services.BacklogService>();
            services.AddScoped<TaskPilot.Services.Assignment.ITeamSnapshotService, TaskPilot.Services.Assignment.TeamSnapshotService>();
            services.AddScoped<TaskPilot.Services.Assignment.ICapacityValidationService, TaskPilot.Services.Assignment.CapacityValidationService>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.SkillScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.AvailabilityScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.VelocityScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.ExperienceScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IAssignmentScoringService, TaskPilot.Services.Assignment.AssignmentScoringService>();
            services.AddScoped<TaskPilot.Services.Assignment.IAssignmentExplanationService, TaskPilot.Services.Assignment.AssignmentExplanationService>();
            services.AddScoped<TaskPilot.Services.Assignment.IAssignmentConfirmationService, TaskPilot.Services.Assignment.AssignmentConfirmationService>();
            // الآن التكوين (configuration) متاح للاستخدام
            services.Configure<AssignmentOptions>(configuration.GetSection(AssignmentOptions.SectionName));
            services.Configure<TaskPilot.Services.Assignment.ScoringWeights>(configuration.GetSection("Assignment:Scoring"));
            services.Configure<TaskPilot.Models.Configurations.RequirementValidationOptions>(configuration.GetSection("RequirementValidation"));

            // ── External ──

            //for ---CV
            services.AddScoped<ICvService, CvService>();
            services.AddScoped<ICvConfirmationService, CvConfirmationService>();
            services.AddScoped<IFileTextExtractor, FileTextExtractor>();

            services.AddScoped<ISkillService, SkillService>();
            services.AddScoped<ISkillMigrationService, SkillMigrationService>();
            services.AddScoped<ISkillRepository, SkillRepository>();
            services.AddScoped<ISprintRetrospectiveService, SprintRetrospectiveService>();
            services.AddScoped<TaskPilot.Services.Implementations.SprintDataCollectionService>();

            services.AddScoped<ISprintLifecycleService, SprintLifecycleService>();
            services.AddScoped<IProjectTeamService, ProjectTeamService>();
            services.AddScoped<ICompanyService, CompanyService>();
            services.AddScoped<ITaskStatusService, TaskPilot.Services.Implementations.TaskStatusService>();
            services.AddScoped<ICompanyService, CompanyService>();
            services.AddScoped<IEmployeeDeactivationService, TaskPilot.Services.Implementations.EmployeeDeactivationService>();
            services.AddScoped<ISprintSelectionService, SprintSelectionService>();

            services.AddHostedService<BackgroundJobs.SubscriptionExpiryJob>();

            services.AddScoped<ISprintRiskService, SprintRiskService>();
            services.AddTransient<TaskPilot.Services.BackgroundJobs.SprintRiskDetectionJob>();

            services.AddTransient<TaskPilot.Services.BackgroundJobs.SprintCompletionJob>();
            //Current User Service
            services.AddHttpContextAccessor();
            return services;
        }
    }
}
