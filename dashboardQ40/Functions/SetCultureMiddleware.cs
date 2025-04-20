using System.Globalization;

namespace dashboardQ40.Functions
{
    public class SetCultureMiddleware
    {
        private readonly RequestDelegate _next;

        public SetCultureMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var cultura = context.Session.GetString("cultura") ?? "es-ES";
            var cultureInfo = new CultureInfo(cultura);

            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            await _next(context);
        }
    }
}
