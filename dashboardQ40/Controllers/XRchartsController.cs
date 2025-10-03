using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using dashboardQ40.Services;
using dashboardQ40.Models;
using System.Globalization;

using static dashboardQ40.Models.Models;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Numerics;

namespace dashboardQ40.Controllers
{
    [Route("XRcharts")]
    public class XRchartsController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;
        private readonly ILogger<DashboardController> _logger;
        private readonly IConfiguration _configuration;

        public XRchartsController(
            IOptions<WebServiceSettings> settings,
            AuthService authService,
            IConfiguration configuration,
            ILogger<DashboardController> logger)
        {
            _settings = settings.Value;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        // Helpers cortos para no repetir
        private string Company => _configuration.GetConnectionString("company");
        private string ConnStr => _configuration.GetConnectionString("CaptorConnection");

        private static IEnumerable<Dictionary<string, object?>> ToRows(DataTable dt)
            => dt.AsEnumerable()
                 .Select(r => dt.Columns.Cast<DataColumn>()
                 .ToDictionary(c => c.ColumnName, c => r[c] == DBNull.Value ? null : r[c]));

        // --------------------------------------------------------------------
        // VIEW
        // --------------------------------------------------------------------
        [HttpGet("")]
        public async Task<IActionResult> IndexAsync()
        {

            var token = await _authService.ObtenerTokenCaptor(_settings.Company);
            if (token != null)
            {
                HttpContext.Session.SetString("AuthToken", token.access_token); // Guardar en sesión
            }

            
            var ListCompanies = new List<result_companies>();            
            var companies = new List<CompanyOption>();

            if (token != null)
            {

                Task<result_Q_Companies> dataResultComp = getDataQuality.getCompanies(
                        token.access_token.ToString(),
                        _settings.QueryCompany,
                        _settings.Company,
                        _settings.trazalog);
                await Task.WhenAll(dataResultComp);

                if (dataResultComp.Result.result != null)
                {
                    foreach (var item in dataResultComp.Result.result)
                    {
                        CultureInfo ci;
                        RegionInfo ri;
                        try
                        {
                            ci = new CultureInfo(item.culture);   // p.ej. "es-MX"
                            ri = new RegionInfo(ci.Name);         // p.ej. "MX"
                        }
                        catch
                        {
                            ci = CultureInfo.InvariantCulture;
                            ri = new RegionInfo("US");            // fallback
                        }

                        companies.Add(new CompanyOption
                        {
                            Company = item.company,
                            CompanyName = item.companyName,
                            Culture = ci.Name,
                            CountryCode = ri.TwoLetterISORegionName
                        });
                    }

                }              


            }

            var countries = companies
                   .GroupBy(c => c.CountryCode)
                   .Select(g =>
                   {
                       var r = new RegionInfo(g.Key); // admite "MX","US","ES"
                       return new { Code = g.Key, Name = r.NativeName }; // "México", "Estados Unidos"
                   })
                   .OrderBy(x => x.Name)
                   .ToList();

            // Simulando datos para los selectores
            
            

            ViewBag.Companies = companies;                 // lista completa
            ViewBag.Countries = countries;                 // países únicos
            ViewBag.CompaniesJson = JsonConvert.SerializeObject(companies);

            
                        ViewBag.produccion = _settings.Produccion;

            return View();
        }


        [HttpGet("ObtenerLineas_XR")]
        public async Task<JsonResult> ObtenerLineas_XR(string company)
        {
            string token;
            if (company != _settings.Company) // significa que es una compañia diferente a la base y bamos por el token de la compañia
            {
                var result = await _authService.ObtenerTokenCaptor(company);
                if (result != null)
                {
                    HttpContext.Session.SetString("AuthToken", result.access_token); // Guardar en sesión
                }
                token = result.access_token;  // Usamos el string del token
            }
            else
            {
                token = HttpContext.Session.GetString("AuthToken");
            }

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(company))
                return Json(Array.Empty<object>());

            // Llama a tu servicio; ajusta nombres de método y settings
            var resp = await getDataQuality.getLinesByCompany(
                token,
                _settings.QueryLineas + company, // tu query
                company,                  // si lo pides, o quítalo
                _settings.trazalog                             // filtro de company
            );

            var list = resp?.result?.Select(w => new {
                workplace = w.workplace,           // id
                workplaceName = w.workplaceName    // nombre
            }) ?? Enumerable.Empty<object>();

            return Json(list);
        }

        [HttpGet("ObtenerVarY_XR")]
        public async Task<JsonResult> ObtenerVarY_XR(string sku, DateTime startDate, DateTime endDate, string line, string planta)
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { value = Array.Empty<object>(), error = "Token no disponible" });

            // Trae filas crudas de controles Y
            var dataTask = getDataQuality.getVarYRows(
                token,
                _settings.QueryVarY + planta,
                planta,
                sku,
                startDate,
                endDate,
                line
            );

            await Task.WhenAll(dataTask);

            var rows = (dataTask.Result?.result ?? Enumerable.Empty<YRawRow>());

            // Agrupa por operación y arma {value, name}
            var items = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.controlOperation))
                .GroupBy(r => new
                {
                    Op = (r.controlOperation ?? "").Trim().ToUpper(),
                    Name = (r.controlOperationName ?? "").Trim()
                })
                .Select(g => new
                {
                    value = g.Key.Op,                  // código para el dropdown
                    name = string.IsNullOrEmpty(g.Key.Name) ? g.Key.Op : g.Key.Name  // texto visible
                })
                .OrderBy(x => x.name)
                .ToList();

            return Json(new { value = items });
        }


        [HttpGet("ObtenerVarX_XR")]
        public async Task<JsonResult> ObtenerVarX_XR(
            string sku, string varY, DateTime fechaInicial, DateTime fechaFinal, string lineaId, string planta)
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { value = Array.Empty<object>(), error = "Token no disponible" });

            // Prefijo para buscar X relacionadas con la Y (incluye a la Y cuando aplica)
            var prefix = (varY?.Length >= 3 ? varY.Substring(0, 3) : varY ?? "") + "%";

            // Trae posibles X ligadas a esa Y
            var opsTask = getDataQuality.getVarXByvarY(
                token, _settings.QueryVarX + planta, planta,
                sku, prefix, fechaInicial, fechaFinal, lineaId);

            await Task.WhenAll(opsTask);

            var opsRaw = opsTask.Result?.result ?? new List<result_varY>();

            // DEDUP por código y map a {value, name}
            var xs = opsRaw
                .Where(o => !string.IsNullOrWhiteSpace(o.controlOperation))
                .GroupBy(o => o.controlOperation!.Trim().ToUpper())
                .Select(g =>
                {
                    var first = g.First();
                    var code = g.Key;
                    var name = (first.controlOperationName ?? "").Trim();
                    return new
                    {
                        value = code,
                        name = string.IsNullOrEmpty(name) ? code : name
                    };
                })
                .OrderBy(x => x.name)
                .ToList();

            // Asegura que la Y aparezca como opción en X (primera posición)
            var yName = opsRaw
                .FirstOrDefault(o => string.Equals(o.controlOperation?.Trim(), varY?.Trim(), StringComparison.OrdinalIgnoreCase))
                ?.controlOperationName;

            var yOption = new
            {
                value = (varY ?? "").Trim().ToUpper(),
                name = string.IsNullOrWhiteSpace(yName) ? $"[Y] {varY}" : $"[Y] {yName.Trim()}"
            };

            // Si ya viene en la lista, reemplázala por la versión con etiqueta [Y]; si no, la insertamos al inicio
            xs = xs.Where(x => !string.Equals(x.value, yOption.value, StringComparison.OrdinalIgnoreCase))
                   .Prepend(yOption)
                   .ToList();

            return Json(new { value = xs });
        }

        [HttpGet("ObtenerProductos_XR")]
        public async Task<JsonResult> ObtenerProductos_XR(string lineaId, DateTime fechaInicial, DateTime fechaFinal, string planta)
        {
            string token = HttpContext.Session.GetString("AuthToken"); // Obtener el token de la sesión

            if (string.IsNullOrEmpty(token))
            {
                return Json(new { error = "Token no disponible" });
            }
            else
            {


                var dataResultP = getDataQuality.getProductsByLine(
                        token.ToString(),
                        _settings.QuerySKUs + planta,
                        planta,
                        lineaId,
                        fechaInicial,
                        fechaFinal);
                await Task.WhenAll(dataResultP);

                var resultado = Json(dataResultP.Result.result);
                return resultado;

            }

        }




        // --------------------------------------------------------------------
        // 1) VARIABLES (para llenar dropdowns de la vista)
        // GET /XRcharts/variables?from=2025-06-01&to=2025-06-30&workplace=L7&reference=COCA...
        // --------------------------------------------------------------------
        [HttpGet("variables")]
        public IActionResult GetVariables(
            DateTime? from,
            DateTime? to,
            string? workplace,
            string? reference)
        {
            try
            {
                var f1 = from ?? DateTime.UtcNow.Date.AddDays(-30);
                var f2 = to ?? DateTime.UtcNow.Date;

                var dt = XRchartsService.GetVariables(
                    "", f1, f2, ConnStr, workplace, reference);

                return Json(ToRows(dt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetVariables");
                return Problem("Error obteniendo variables.");
            }
        }

        // --------------------------------------------------------------------
        // 2) FILAS BASE (lecturas crudas con LSL/USL/Target, subgroupId, etc.)
        // GET /XRcharts/base?from=...&to=...&workplace=...&reference=...&controlOperation=...
        // --------------------------------------------------------------------
        [HttpGet("base")]
        public IActionResult GetBaseRows(
            DateTime? from,
            DateTime? to,
            string? workplace,
            string? reference,
            string? controlOperation, string planta)
        {
            try
            {
                var f1 = from ?? DateTime.UtcNow.Date.AddDays(-30);
                var f2 = to ?? DateTime.UtcNow.Date;

                var dt = XRchartsService.GetXRBaseRows(
                    planta, f1, f2, ConnStr, workplace, reference, controlOperation);

                return Json(ToRows(dt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetBaseRows");
                return Problem("Error obteniendo datos base.");
            }
        }

        // --------------------------------------------------------------------
        // 3) STATS DE SUBGRUPO (Xbar/S por subgroupId)
        // GET /XRcharts/subgroups?from=...&to=...&workplace=...&reference=...&controlOperation=...
        // --------------------------------------------------------------------
        [HttpGet("subgroups")]
        public IActionResult GetSubgroupStats(
            DateTime? from,
            DateTime? to,
            string? workplace,
            string? reference,
            string? controlOperation)
        {
            try
            {
                var f1 = from ?? DateTime.UtcNow.Date.AddDays(-30);
                var f2 = to ?? DateTime.UtcNow.Date;

                var baseDt = XRchartsService.GetXRBaseRows(
                    "", f1, f2, ConnStr, workplace, reference, controlOperation);

                var stats = XRchartsService.BuildSubgroupStats(baseDt);
                return Json(ToRows(stats));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetSubgroupStats");
                return Problem("Error calculando estadísticas de subgrupos.");
            }
        }

        // --------------------------------------------------------------------
        // 4) CAPABILITY POR VARIABLE (Cp/Cpk y Pp/Ppk)
        // GET /XRcharts/capability?from=...&to=...&workplace=...&reference=...&controlOperation=...
        //    *controlOperation opcional: si lo envías, filtra a una sola variable*
        // --------------------------------------------------------------------
        [HttpGet("capability")]
        public IActionResult GetCapability(
            DateTime? from,
            DateTime? to,
            string? workplace,
            string? reference,
            string? controlOperation, string planta)
        {
            try
            {
                var f1 = from ?? DateTime.UtcNow.Date.AddDays(-30);
                var f2 = to ?? DateTime.UtcNow.Date;

                var baseDt = XRchartsService.GetXRBaseRows(
                    planta, f1, f2, ConnStr, workplace, reference, controlOperation);

                var cap = XRchartsService.BuildCapability(baseDt);
                return Json(ToRows(cap));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetCapability");
                return Problem("Error calculando capability.");
            }
        }
    }
}
