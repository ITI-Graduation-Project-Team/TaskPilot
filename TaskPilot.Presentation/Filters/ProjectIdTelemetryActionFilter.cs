using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Threading.Tasks;
using TaskPilot.Services.Helpers;

namespace TaskPilot.Presentation.Filters
{
    public class ProjectIdTelemetryActionFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            Guid? projectId = null;

            if (context.RouteData.Values.TryGetValue("projectId", out var routeVal) && routeVal != null)
            {
                if (Guid.TryParse(routeVal.ToString(), out var pId)) projectId = pId;
            }
            else if (context.RouteData.Values.TryGetValue("id", out routeVal) && routeVal != null)
            {
                var controllerName = context.ActionDescriptor.RouteValues["controller"];
                if (controllerName != null && controllerName.Contains("Project", StringComparison.OrdinalIgnoreCase))
                {
                    if (Guid.TryParse(routeVal.ToString(), out var pId)) projectId = pId;
                }
            }

            if (!projectId.HasValue && context.HttpContext.Request.Query.TryGetValue("projectId", out var queryVal))
            {
                if (Guid.TryParse(queryVal.ToString(), out var pId)) projectId = pId;
            }

            using (AiTelemetryContext.SetProjectId(projectId))
            {
                await next();
            }
        }
    }
}
