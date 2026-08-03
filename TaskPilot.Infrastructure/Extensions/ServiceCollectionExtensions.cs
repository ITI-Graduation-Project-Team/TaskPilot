using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Infrastructure.Services.Email;
using TaskPilot.Infrastructure.Services.Google;
using TaskPilot.Infrastructure.Services.Storage;
using TaskPilot.Infrastructure.Services.Token;
using TaskPilot.Infrastructure.Settings;
using TaskPilot.Services.Interfaces.External;
using TaskPilot.Services.Interfaces.ExternalServicesInterfaces;

namespace TaskPilot.Infrastructure.Extensions
{
    public static class
        ServiceCollectionExtensions
    {
        public static IServiceCollection
            AddInfrastructure(
                this IServiceCollection services,
                IConfiguration configuration)
        {
            services.AddScoped<
                IFileStorageService,
                CloudinaryService>();
            services.AddScoped<
                TaskPilot.AI.Services.Interfaces.IAiAssetStorageService,
                CloudinaryAiAssetStorageService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IEmailBodyService, EmailBodyService>();
            services.AddScoped<ITokenService, JWTService>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            services.AddScoped<IGoogleAuthService, GoogleAuthService>();


            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.Configure<JWTSettings>(configuration.GetSection("JWTSettings"));
            services.Configure<GoogleSettings>(configuration.GetSection("GoogleSettings"));
            return services;
        }
    }
}