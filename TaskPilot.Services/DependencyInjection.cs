using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration; // ضروري جداً
using TaskPilot.Services.Helpers;
using TaskPilot.Services.Interfaces;

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
            

            // الآن التكوين (configuration) متاح للاستخدام
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.Configure<JWTSettings>(configuration.GetSection("JWTSettings"));
            services.Configure<GoogleSettings>(configuration.GetSection("GoogleSettings"));
            // ── External ──

            services.AddScoped<ITokenService, JWTService>();

            return services;
        }
    }
}