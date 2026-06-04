using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace TaskPilot.Presentation
{
    public class LanguageHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
            {
                operation.Parameters = new List<IOpenApiParameter>();
            }

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "lang",
                In = ParameterLocation.Header,
                Description = "Language preference (e.g., en, ar)",
                Required = false,
                Schema = context.SchemaGenerator.GenerateSchema(typeof(string), context.SchemaRepository)
            });
        }
    }
}