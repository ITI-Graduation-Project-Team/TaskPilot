using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;
using TaskPilot.Services.Interfaces.External;

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
            services.AddScoped<IWbsPersistenceService, WbsPersistenceService>();
            services.AddScoped<IBacklogService, BacklogService>();
            services.AddScoped<IBacklogRegenerationService, BacklogRegenerationService>();
            services.AddScoped<ISprintPlanningService, SprintPlanningService>();
            
            // الآن التكوين (configuration) متاح للاستخدام
          
            // ── External ──

            //for ---CV
            services.AddScoped<ICvService, CvService>();
            services.AddScoped<IFileTextExtractor, FileTextExtractor>();

            services.AddScoped<ISkillService, SkillService>();


            services.AddScoped<ICompanyService,CompanyService>();
            services.AddScoped(typeof(IRepository<>),
                   typeof(Repository<>));

            //Current User Service
            services.AddHttpContextAccessor();
            return services;
        }
    }
}