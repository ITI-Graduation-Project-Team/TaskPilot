using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Data.Context;
using TaskPilot.Data.Identity;
using TaskPilot.Data.Repositories;
using TaskPilot.Models.Common;
using TaskPilot.Models.Entities;

namespace TaskPilot.Data
{
    /// <summary>
    /// Registers all Data-layer services into the DI container.
    /// Called from Program.cs via <c>builder.Services.AddData(configuration)</c>.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddData(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<User, IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            services.Configure<IdentityOptions>(options =>
            {
                  options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultPhoneProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultPhoneProvider;
                options.Tokens.ChangeEmailTokenProvider = TokenOptions.DefaultPhoneProvider;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;
              
            });
         
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IUnitOfWork>(sp =>
                        sp.GetRequiredService<ApplicationDbContext>());

            return services;
        }
    }
}
