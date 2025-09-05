using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace dashboardQ40.Middlewares
{
    public class SetCultureMiddleware
    {
        private readonly RequestDelegate _next;

        public SetCultureMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            // Si hay cultura en sesión, sincronízala en la cookie que usa RequestLocalization.
            var culture = context.Session.GetString("culture");

            if (!string.IsNullOrWhiteSpace(culture))
            {
                var cookieName = CookieRequestCultureProvider.DefaultCookieName;
                var desiredValue = CookieRequestCultureProvider
                    .MakeCookieValue(new RequestCulture(culture));

                // Solo escribe si no existe o cambió (evita reescrituras innecesarias).
                if (!context.Request.Cookies.TryGetValue(cookieName, out var current) ||
                    !string.Equals(current, desiredValue, StringComparison.Ordinal))
                {
                    context.Response.Cookies.Append(
                        cookieName,
                        desiredValue,
                        new CookieOptions
                        {
                            Path = "/",
                            IsEssential = true,
                            Expires = DateTimeOffset.UtcNow.AddYears(1)
                        });
                }
            }

            // No cambies CultureInfo aquí: deja que UseRequestLocalization la fije.
            await _next(context);
        }
    }
}
