using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace TaskPilot.Presentation.Middlewares
{
    public class LanguageMiddleware
    {
        private readonly RequestDelegate _next;

        public LanguageMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var lang = context.Request.Headers["lang"].ToString();
            if (string.IsNullOrEmpty(lang))
            {
                lang = "en";
            }

            try
            {
                var culture = new CultureInfo(lang);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (CultureNotFoundException)
            {
                // Fallback if invalid lang
                var culture = new CultureInfo("en");
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }

            await _next(context);
        }
    }
}
