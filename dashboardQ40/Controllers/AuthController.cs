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
        var correlationId = HttpContext.TraceIdentifier;

        _logger.LogInformation(
            "ValidarUsuario iniciado | user={User} | company={Company} | cid={CID}",
            user, company, correlationId);

        try
        {
            // 1) Obtener token (por ahora siempre con company base)
            var token = await _authService.ObtenerTokenCaptor(_settings.Company);

            if (token == null)
            {
                _logger.LogWarning("No se obtuvo token | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);
                return Json(new { autorizado = false });
            }

            var masked = token.access_token?.Length > 6
                ? $"***{token.access_token[^6..]}"
                : "***";

            _logger.LogInformation(
                "Token obtenido | expira_en={ExpSeconds} | token={Masked} | cid={CID}",
                token.expires_in, masked, correlationId);

            HttpContext.Session.SetString("AuthToken", token.access_token);

            // 2) Llamar al WS getAuthUser (que ya trae el query extendido)
            var endpoint = _settings.BaseUrl + _settings.QueryAuthUser + _settings.Company;
            _logger.LogInformation(
                "Invocando getAuthUser | endpoint={Endpoint} | company={Company} | user={User} | cid={CID}",
                endpoint, company, user, correlationId);

            var wsResult = await AuthService.getAuthUser(
                token.access_token,
                endpoint,
                company,
                user);

            if (wsResult == null)
            {
                _logger.LogError("Respuesta nula del WS | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);
                return Json(new { autorizado = false });
            }

            _logger.LogDebug("WS getAuthUser result={@WSResult} | cid={CID}", wsResult, correlationId);

            result_authUser? firstUser = null;
            var permisos = new List<DashboardProgramPermission>();

            if (wsResult.result != null && wsResult.result.Any())
            {
                foreach (var item in wsResult.result)
                {
                    // Log por cada fila devuelta
                    _logger.LogInformation(
                        "Fila WS | appUser={AppUser} | role={Role} | programGroup={PG} | programGroupName={PGN} | cid={CID}",
                        item.appUser, item.role, item.programGroup, item.programGroupName, correlationId);

                    // 2.1 Tomar la primera fila como "identidad" del usuario
                    if (firstUser == null)
                    {
                        firstUser = new result_authUser
                        {
                            appUser = item.appUser,
                            appUserName = item.appUserName,
                            culture = item.culture,
                            company = item.company,
                            role = item.role,
                            programGroup = item.programGroup,
                            programGroupName = item.programGroupName,
                            canread = item.canread,
                            caninsert = item.caninsert,
                            canmodify = item.canmodify,
                            candelete = item.candelete
                        };

                        HttpContext.Session.SetString("culture", item.culture ?? "es-ES");
                    }

                    // 2.2 Construir el permiso para este programGroup
                    if (!string.IsNullOrWhiteSpace(item.programGroupName))
                    {
                        var perm = new DashboardProgramPermission
                        {
                            ProgramGroup = item.programGroup,
                            ProgramGroupName = item.programGroupName,
                            Global = item.canread,
                            Country = item.caninsert,
                            Planta = item.canmodify,
                            Modify = item.candelete
                        };

                        permisos.Add(perm);
                    }
                }
            }
            else
            {
                _logger.LogWarning("WS retornó 'result' vacío | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);
            }

            // 3) Si encontramos usuario y permisos, guardarlos en Session
            if (firstUser != null)
            {
                HttpContext.Session.SetString("usuarioNombre", firstUser.appUserName);
                HttpContext.Session.SetString("usuario", firstUser.appUser);
                HttpContext.Session.SetString("compania", firstUser.company);
                HttpContext.Session.SetString("culture", firstUser.culture ?? "es-ES");
                HttpContext.Session.SetString("role", firstUser.role ?? string.Empty);

                // Serializar lista de permisos a JSON y guardarla en Session
                var permisosJson = JsonSerializer.Serialize(permisos);
                HttpContext.Session.SetString("permisosDashboard", permisosJson);

                _logger.LogInformation(
                    "Usuario autorizado | appUser={AppUser} | name={Name} | company={Company} | culture={Culture} | cid={CID}",
                    firstUser.appUser, firstUser.appUserName, firstUser.company, firstUser.culture, correlationId);

                // Opcional: regresamos también alguna info al front si la necesitas
                return Json(new
                {
                    autorizado = true,
                    usuario = firstUser.appUser,
                    nombre = firstUser.appUserName,
                    company = firstUser.company,
                    culture = firstUser.culture,
                    role = firstUser.role
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
