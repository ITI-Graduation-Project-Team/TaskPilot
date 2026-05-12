using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Data.Repositories;
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;
using TaskPilot.Services.Interfaces.CVExtractorInterfaces;

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
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IEmailBodyService, EmailBodyService>();
            services.AddScoped<IGoogleAuthService, GoogleAuthService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
            services.AddScoped<IUserSubscriptionService, UserSubscriptionService>();
            
            // الآن التكوين (configuration) متاح للاستخدام
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.Configure<JWTSettings>(configuration.GetSection("JWTSettings"));
            services.Configure<GoogleSettings>(configuration.GetSection("GoogleSettings"));
            // ── External ──

            services.AddScoped<ITokenService, JWTService>();
            //for ---CV
            services.AddScoped<ICvService, CvService>();
            services.AddScoped<ICvParserService, OpenAiCvParserService>();
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