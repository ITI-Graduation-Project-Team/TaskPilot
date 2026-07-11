using TaskPilot.Models.Enums;

namespace TaskPilot.Services.Interfaces.Payments
{
    public interface IPaymentGatewayFactory
    {
        IPaymentGateway GetGateway(TaskPilot.Models.Enums.PaymentGateway gatewayType);
    }
}
