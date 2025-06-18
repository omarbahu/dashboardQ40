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
            var culture = context.Session.GetString("culture") ?? "es-ES";

            try
            {
                var cultureInfo = new CultureInfo(culture);
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;
            }
            catch (CultureNotFoundException)
            {
                var defaultCulture = new CultureInfo("es-ES");
                CultureInfo.CurrentCulture = defaultCulture;
                CultureInfo.CurrentUICulture = defaultCulture;
            }

            await _next(context);
        }
    }
}
