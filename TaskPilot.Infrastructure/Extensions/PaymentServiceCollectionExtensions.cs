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

            services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
            services.Configure<PayPalOptions>(configuration.GetSection(PayPalOptions.SectionName));

            services.AddScoped<IPaymentGateway, StripeGateway>();
            services.AddScoped<IPaymentGateway, PayPalGateway>();
            services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
            services.AddScoped<IWebhookService, WebhookService>();

            return services;
        }
    }
}
