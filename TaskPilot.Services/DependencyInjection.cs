using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Services
{
    /// <summary>
    /// Registers all Services-layer into the DI container.
    /// Called from Program.cs via <c>builder.Services.AddServices()</c>.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddServices(this IServiceCollection services)
        {
            // ── Business Services ──
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
