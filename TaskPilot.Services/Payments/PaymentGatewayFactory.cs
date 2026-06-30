using System;
using System.Collections.Generic;
using System.Linq;
using TaskPilot.Models.Enums;
using TaskPilot.Services.Interfaces.Payments;

namespace TaskPilot.Services.Payments
{
    public class PaymentGatewayFactory : IPaymentGatewayFactory
    {
        private readonly Dictionary<TaskPilot.Models.Enums.PaymentGateway, IPaymentGateway> _gateways;

        public PaymentGatewayFactory(IEnumerable<IPaymentGateway> gateways)
        {
            _gateways = gateways.ToDictionary(g => g.GatewayType);
        }

        public IPaymentGateway GetGateway(TaskPilot.Models.Enums.PaymentGateway gatewayType)
        {
            if (_gateways.TryGetValue(gatewayType, out var gateway))
            {
                return gateway;
            }

            throw new NotSupportedException($"Payment gateway {gatewayType} is not supported.");
        }
    }
}
