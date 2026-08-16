using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TaskPilot.Models.Common.Results;
using TaskPilot.Presentation.Models;
using TaskPilot.Services.Interfaces;

namespace TaskPilot.Presentation.Filters
{
    public class TokenQuotaActionFilter : IAsyncActionFilter
    {
        private readonly ITokenQuotaContext _tokenQuotaContext;

        public TokenQuotaActionFilter(ITokenQuotaContext tokenQuotaContext)
        {
            _tokenQuotaContext = tokenQuotaContext;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();
            
            Console.WriteLine($"[TokenQuotaActionFilter] After next(). Context Hash: {_tokenQuotaContext.GetHashCode()}, LimitReached: {_tokenQuotaContext.LimitReached}, Exception: {executedContext.Exception?.Message}");

            if (_tokenQuotaContext.LimitReached)
            {
                executedContext.ExceptionHandled = true; // prevent raw 500 if agent threw
                
                var response = ApiResponse.Fail("TOKEN_LIMIT_REACHED", "AI token limit reached for this billing period.");
                if (response.Errors != null && response.Errors.Count > 0)
                {
                    response.Errors[0] = new ErrorDetail
                    {
                        Code = response.Errors[0].Code,
                        Description = response.Errors[0].Description,
                        Metadata = new Dictionary<string, object>
                        {
                            { "CurrentUsage", _tokenQuotaContext.CurrentUsage },
                            { "Limit", _tokenQuotaContext.Limit }
                        }
                    };
                }
                
                executedContext.Result = new ObjectResult(response)
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
