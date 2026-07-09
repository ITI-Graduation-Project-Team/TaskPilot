using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;
using TaskPilot.Models.Common;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;
using TaskPilot.Services.Interfaces.External;
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
            
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
            services.AddScoped<IUserSubscriptionService, UserSubscriptionService>();
            services.AddScoped<ILocalizationService, LocalizationService>();
            services.AddScoped<IRequirementFinalizationService, RequirementFinalizationService>();
            services.AddScoped<IWbsSkillEnrichmentService, WbsSkillEnrichmentService>();
            services.AddScoped<IWbsPersistenceService, WbsPersistenceService>();
            services.AddScoped<IBacklogService, BacklogService>();
            services.AddScoped<IBacklogRegenerationService, BacklogRegenerationService>();
            services.AddScoped<ISprintPlanningService, SprintPlanningService>();
            services.AddScoped<ISprintConfirmationService, SprintConfirmationService>();
            services.AddScoped<ITechStackService, TechStackService>();
            services.AddScoped<IWbsGenerationService, WbsGenerationService>();
            services.AddScoped<TaskPilot.Services.Assignment.ITeamSnapshotService, TaskPilot.Services.Assignment.TeamSnapshotService>();
            services.AddScoped<TaskPilot.Services.Assignment.ICapacityValidationService, TaskPilot.Services.Assignment.CapacityValidationService>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.SkillScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.AvailabilityScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.VelocityScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IScoreCalculator, TaskPilot.Services.Assignment.ExperienceScoreCalculator>();
            services.AddScoped<TaskPilot.Services.Assignment.IAssignmentScoringService, TaskPilot.Services.Assignment.AssignmentScoringService>();
            
            // الآن التكوين (configuration) متاح للاستخدام
            services.Configure<AssignmentOptions>(configuration.GetSection(AssignmentOptions.SectionName));
            services.Configure<TaskPilot.Services.Assignment.ScoringWeights>(configuration.GetSection("Assignment:Scoring"));
          
            // ── External ──

            //for ---CV
            services.AddScoped<ICvService, CvService>();
            services.AddScoped<ICvConfirmationService, CvConfirmationService>();
            services.AddScoped<IFileTextExtractor, FileTextExtractor>();

            services.AddScoped<ISkillService, SkillService>();
            services.AddScoped<ISkillRepository, SkillRepository>();
            services.AddScoped<ISprintRetrospectiveService, SprintRetrospectiveService>();

            services.AddScoped<IProjectTeamService, ProjectTeamService>();
            services.AddScoped<ICompanyService,CompanyService>();

            services.AddHostedService<BackgroundJobs.SubscriptionExpiryJob>();

            //Current User Service
            services.AddHttpContextAccessor();
            return services;
        }
    }
}