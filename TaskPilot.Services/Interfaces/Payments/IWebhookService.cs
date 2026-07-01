using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using TaskPilot.Models.Common.Results;

namespace TaskPilot.Services.Interfaces.Payments
{
    public interface IWebhookService
    {
        Task<Result> HandleWebhookAsync(string gatewayName, string payload, IHeaderDictionary headers);
    }
}
