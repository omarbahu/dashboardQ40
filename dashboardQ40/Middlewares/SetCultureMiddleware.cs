using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Threading.Tasks;

namespace dashboardQ40.Middlewares
{
    public class SetCultureMiddleware
    {
        private readonly RequestDelegate _next;

        public SetCultureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Obtener cultura guardada en sesión (ej: "es-ES" o "en-US")
            var culture = context.Session.GetString("culture");

            if (!string.IsNullOrEmpty(culture))
            {
                var cultureInfo = new CultureInfo(culture);

                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;
            }

            await _next(context);
        }
    }
}
