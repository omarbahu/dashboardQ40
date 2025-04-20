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

        public AuthController(IOptions<WebServiceSettings> settings, AuthService authService, IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _authService = authService;
        }

        [HttpGet]
        public async Task<IActionResult> ValidarUsuario(string user, string company)
        {
         
            var token = await _authService.ObtenerTokenCaptor();
            if (token != null)
            {
                HttpContext.Session.SetString("AuthToken", token.access_token); // Guardar en sesión
            }
            if (token != null)
            {
                var dataResultP = AuthService.getAuthUser(
                       token.access_token.ToString(), _settings.QueryAuthUser, company,
                       user);
                await Task.WhenAll(dataResultP);
                var ListAuthUser = new List<result_authUser>();

                if (dataResultP.Result.result != null)
                {
                    foreach (var item in dataResultP.Result.result)
                    {
                        var AuthUser = new result_authUser
                        {
                            appUser = item.appUser,
                            appUserName = item.appUserName,
                            culture = item.culture
                        };

                        ListAuthUser.Add(AuthUser);
                    }

                }
                if (ListAuthUser.Any())
                {
                    HttpContext.Session.SetString("usuarioNombre", ListAuthUser.First().appUserName);
                    HttpContext.Session.SetString("usuario", ListAuthUser.First().appUser);
                    HttpContext.Session.SetString("compania", company);
                    HttpContext.Session.SetString("cultura", ListAuthUser.First().culture ?? "es-ES");

                    return Json(new
                    {
                        autorizado = true,
                        usuario = ListAuthUser.First().appUser,
                        nombre = ListAuthUser.First().appUserName
                    });
                }
                else
                {
                    return Json(new { autorizado = false });
                }
            }
            else
            {
                return Json(new { autorizado = false });
            }
        }

        public class ValidacionResultado
        {
            public bool EsValido { get; set; }
        }
    }
}
