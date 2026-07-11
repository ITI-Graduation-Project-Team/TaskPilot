using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaskPilot.Infrastructure.Options.Payments;
using TaskPilot.Infrastructure.Payments.Gateways;
using TaskPilot.Services.Interfaces.Payments;
using TaskPilot.Services.Payments;

namespace TaskPilot.Infrastructure.Extensions
{
    public static class PaymentServiceCollectionExtensions
    {
        public static IServiceCollection AddPaymentLayer(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpClient();

            services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.SectionName));
            services.Configure<PaymobOptions>(configuration.GetSection(PaymobOptions.SectionName));

            services.AddScoped<IPaymentGateway, PayPalGateway>();
            services.AddScoped<IPaymentGateway, PaymobGateway>();
            services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
            services.AddScoped<IWebhookService, WebhookService>();

            return services;
        }
    }
}
