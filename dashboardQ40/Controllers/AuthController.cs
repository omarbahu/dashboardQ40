using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Controllers
{
    public class AuthController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger; // 👈

        public AuthController(
       IOptions<WebServiceSettings> settings,
       AuthService authService,
       IHttpClientFactory httpClientFactory,
       ILogger<AuthController> logger) // 👈
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _authService = authService;
            _logger = logger; // 👈
        }

        [HttpGet]
        public async Task<IActionResult> ValidarUsuario(string user, string company)
        {
            // CorrelationId para seguir la traza en todos los logs de esta petición
            var correlationId = HttpContext.TraceIdentifier;

            _logger.LogInformation(
                "ValidarUsuario iniciado | user={User} | company={Company} | cid={CID}",
                user, company, correlationId);

            try
            {
                result_token token = null;
                if (company != _settings.Company) // significa que es una compañia diferente a la base y bamos por el token de la compañia
                {
                    token = await _authService.ObtenerTokenCaptor(company);                    
                }
                else
                {
                    token = await _authService.ObtenerTokenCaptor(_settings.Company);
                }


                if (token == null)
                {
                    _logger.LogWarning("No se obtuvo token | user={User} | company={Company} | cid={CID}",
                        user, company, correlationId);
                    return Json(new { autorizado = false });
                }

                // ⚠️ Nunca loguees el token completo
                var masked = token.access_token?.Length > 6
                    ? $"***{token.access_token[^6..]}"
                    : "***";
                _logger.LogInformation(
                    "Token obtenido | expira_en={ExpSeconds} | token={Masked} | cid={CID}",
                    token.expires_in, masked, correlationId);

                HttpContext.Session.SetString("AuthToken", token.access_token);

                // Llamada al WS
                _logger.LogInformation(
                    "Invocando getAuthUser | endpoint={Endpoint} | company={Company} | user={User} | cid={CID}",
                    _settings.BaseUrl + _settings.QueryAuthUser + company, company, user, correlationId);

                var wsTask = AuthService.getAuthUser(
                    token.access_token, _settings.BaseUrl + _settings.QueryAuthUser + company, company, user);

                var wsResult = await wsTask;

                if (wsResult == null)
                {
                    _logger.LogError("Respuesta nula del WS | user={User} | company={Company} | cid={CID}",
                        user, company, correlationId);
                    return Json(new { autorizado = false });
                }

                // Log a nivel Debug el JSON devuelto por el WS (sin PII sensible)
                _logger.LogDebug("WS getAuthUser result={@WSResult} | cid={CID}", wsResult, correlationId);

                var listAuthUser = new List<result_authUser>();

                if (wsResult.result != null)
                {
                    foreach (var item in wsResult.result)
                    {
                        _logger.LogInformation(
                            "Usuario devuelto por WS | appUser={AppUser} | name={Name} | culture={Culture} | cid={CID}",
                            item.appUser, item.appUserName, item.culture, correlationId);

                        var authUser = new result_authUser
                        {
                            appUser = item.appUser,
                            appUserName = item.appUserName,
                            culture = item.culture
                        };

                        HttpContext.Session.SetString("culture", item.culture ?? "es-ES");
                        listAuthUser.Add(authUser);
                    }
                }
                else
                {
                    _logger.LogWarning("WS retornó 'result' vacío | user={User} | company={Company} | cid={CID}",
                        user, company, correlationId);
                }

                if (listAuthUser.Any())
                {
                    var first = listAuthUser.First();
                    HttpContext.Session.SetString("usuarioNombre", first.appUserName);
                    HttpContext.Session.SetString("usuario", first.appUser);
                    HttpContext.Session.SetString("compania", company);
                    HttpContext.Session.SetString("culture", first.culture ?? "es-ES");

                    _logger.LogInformation(
                        "Usuario autorizado | appUser={AppUser} | name={Name} | culture={Culture} | cid={CID}",
                        first.appUser, first.appUserName, first.culture, correlationId);

                    return Json(new
                    {
                        autorizado = true,
                        usuario = first.appUser,
                        nombre = first.appUserName
                    });
                }

                _logger.LogWarning(
                    "Sin usuarios autorizados tras validar | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);

                return Json(new { autorizado = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Excepción en ValidarUsuario | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);
                return Json(new { autorizado = false });
            }
        }


        public class ValidacionResultado
        {
            public bool EsValido { get; set; }
        }
    }
}
