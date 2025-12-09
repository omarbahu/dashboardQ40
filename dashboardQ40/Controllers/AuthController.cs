using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                "ValidarUsuario initiated | user={User} | company={Company} | cid={CID}",
                user, company, correlationId);

            try
            {
                // ✅ VALIDACIÓN 1: Parámetros requeridos
                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(company))
                {
                    _logger.LogWarning(
                        "Missing required parameters | user={User} | company={Company} | cid={CID}",
                        user, company, correlationId);
                    return Json(new { autorizado = false, mensaje = "User and company are required" });
                }

                // ✅ VALIDACIÓN 2: Caracteres válidos (prevenir inyección)
                if (!IsValidUsername(user))
                {
                    _logger.LogWarning(
                        "Invalid username format | user={User} | cid={CID}",
                        user, correlationId);
                    return Json(new { autorizado = false, mensaje = "Invalid username format" });
                }

                if (!IsValidCompany(company))
                {
                    _logger.LogWarning(
                        "Invalid company format | company={Company} | cid={CID}",
                        company, correlationId);
                    return Json(new { autorizado = false, mensaje = "Invalid company format" });
                }

                // ✅ VALIDACIÓN 3: Longitud máxima
                if (user.Length > 50 || company.Length > 10)
                {
                    _logger.LogWarning(
                        "Username or company too long | user.Length={UserLen} | company.Length={CompLen} | cid={CID}",
                        user.Length, company.Length, correlationId);
                    return Json(new { autorizado = false, mensaje = "Username or company too long" });
                }

                // 1) Obtener token
                var token = await _authService.ObtenerTokenCaptor(_settings.Company);

                if (token == null)
                {
                    _logger.LogWarning(
                        "Failed to obtain token | user={User} | company={Company} | cid={CID}",
                        user, company, correlationId);
                    return Json(new { autorizado = false, mensaje = "Authentication service unavailable" });
                }

                // ✅ Enmascarar token en logs
                var masked = token.access_token?.Length > 6
                    ? $"***{token.access_token[^6..]}"
                    : "***";

                _logger.LogInformation(
                    "Token obtained | expires_in={ExpSeconds} | token={Masked} | cid={CID}",
                    token.expires_in, masked, correlationId);

                // ✅ Guardar token SIN encriptar (intranet segura)
                HttpContext.Session.SetString("AuthToken", token.access_token);

                // 2) Llamar al WS getAuthUser
                var endpoint = _settings.BaseUrl + _settings.QueryAuthUser + _settings.Company;
                _logger.LogInformation(
                    "Calling getAuthUser | endpoint={Endpoint} | company={Company} | user={User} | cid={CID}",
                    endpoint, company, user, correlationId);

                var wsResult = await AuthService.getAuthUser(
                    token.access_token,
                    endpoint,
                    company,
                    user);

                if (wsResult == null)
                {
                    _logger.LogError(
                        "Null response from WS | user={User} | company={Company} | cid={CID}",
                        user, company, correlationId);
                    return Json(new { autorizado = false, mensaje = "Authentication service error" });
                }

                _logger.LogDebug("WS getAuthUser result={@WSResult} | cid={CID}", wsResult, correlationId);

                result_authUser? firstUser = null;
                var permisos = new List<DashboardProgramPermission>();

                if (wsResult.result != null && wsResult.result.Any())
                {
                    foreach (var item in wsResult.result)
                    {
                        // Log reducido en producción (usa LogDebug en vez de LogInformation)
                        _logger.LogDebug(
                            "WS row | appUser={AppUser} | role={Role} | programGroup={PG} | programGroupName={PGN} | cid={CID}",
                            item.appUser, item.role, item.programGroup, item.programGroupName, correlationId);

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

                            // ✅ Validar cultura antes de guardar
                            var culture = item.culture ?? "es-ES";
                            if (!IsValidCulture(culture))
                            {
                                culture = "es-ES";
                            }
                            HttpContext.Session.SetString("culture", culture);
                        }

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
                    _logger.LogWarning(
                        "Empty result from WS | user={User} | company={Company} | cid={CID}",
                        user, company, correlationId);
                }

                // 3) Guardar en Session si hay usuario válido
                if (firstUser != null)
                {
                    HttpContext.Session.SetString("usuarioNombre", firstUser.appUserName ?? string.Empty);
                    HttpContext.Session.SetString("usuario", firstUser.appUser ?? string.Empty);
                    HttpContext.Session.SetString("compania", firstUser.company ?? string.Empty);
                    HttpContext.Session.SetString("culture", firstUser.culture ?? "es-ES");
                    HttpContext.Session.SetString("role", firstUser.role ?? string.Empty);

                    var permisosJson = JsonSerializer.Serialize(permisos);
                    HttpContext.Session.SetString("permisosDashboard", permisosJson);

                    _logger.LogInformation(
                        "User authorized successfully | appUser={AppUser} | name={Name} | company={Company} | cid={CID}",
                        firstUser.appUser, firstUser.appUserName, firstUser.company, correlationId);

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
                    "No authorized users after validation | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);

                return Json(new { autorizado = false, mensaje = "User not authorized" });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "HTTP request failed in ValidarUsuario | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);
                return Json(new { autorizado = false, mensaje = "Authentication service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error in ValidarUsuario | user={User} | company={Company} | cid={CID}",
                    user, company, correlationId);
                return Json(new { autorizado = false, mensaje = "Authentication error" });
            }
        }

       
        /// <summary>
        /// Valida que el username solo contenga caracteres seguros
        /// Permite: letras, números, punto, guión, guión bajo y @
        /// </summary>
        private static bool IsValidUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            return Regex.IsMatch(username, @"^[a-zA-Z0-9._@-]+$");
        }

        /// <summary>
        /// Valida que company solo contenga alfanuméricos
        /// </summary>
        private static bool IsValidCompany(string company)
        {
            if (string.IsNullOrWhiteSpace(company)) return false;
            return Regex.IsMatch(company, @"^[a-zA-Z0-9]+$");
        }

        /// <summary>
        /// Valida que la cultura sea un formato válido (ej: es-ES, en-US)
        /// </summary>
        private static bool IsValidCulture(string culture)
        {
            if (string.IsNullOrWhiteSpace(culture)) return false;
            return Regex.IsMatch(culture, @"^[a-z]{2}-[A-Z]{2}$");
        }

        public class ValidacionResultado
        {
            public bool EsValido { get; set; }
        }

    }
}
