using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Services;
using TaskPilot.AI.Services;
using TaskPilot.AI.Services.Interfaces;

namespace TaskPilot.AI.Extensions
{
    public static class
        ServiceCollectionExtensions
    {
        public static IServiceCollection
            AddAiLayer(
                this IServiceCollection services)
        {
            services.AddSingleton<
                IAiKernelService,
                KernelService>();

            services.AddScoped<
                ICvAiService,
                CvAiService>();

            return services;
        }
    }
}