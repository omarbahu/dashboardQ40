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
            try
            {
                var token = await _authService.ObtenerTokenCaptor(_settings.Company);
                if (token != null)
                {
                    HttpContext.Session.SetString("AuthToken", token.access_token);
                }

                var ListCompanies = new List<result_companies>();
                var companies = new List<CompanyOption>();

                if (token != null)
                {
                    Task<result_Q_Companies> dataResultComp = getDataQuality.getCompanies(
                            token.access_token.ToString(),
                            _settings.BaseUrl + _settings.QueryCompany,
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
                                ci = new CultureInfo(item.culture);
                                ri = new RegionInfo(ci.Name);
                            }
                            catch
                            {
                                ci = CultureInfo.InvariantCulture;
                                ri = new RegionInfo("US");
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
                           var r = new RegionInfo(g.Key);
                           return new { Code = g.Key, Name = r.NativeName };
                       })
                       .OrderBy(x => x.Name)
                       .ToList();

                ViewBag.Companies = companies;
                ViewBag.Countries = countries;
                ViewBag.CompaniesJson = JsonConvert.SerializeObject(companies);
                ViewBag.produccion = _settings.Produccion;

                return View();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in XRcharts IndexAsync");
                ViewBag.ErrorMessage = "Unable to load company data. Please try again.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in XRcharts IndexAsync");
                ViewBag.ErrorMessage = "An unexpected error occurred.";
                return View();
            }
        }


        [HttpGet("ObtenerLineas_XR")]
        public async Task<JsonResult> ObtenerLineas_XR(string company)
        {
            try
            {
                string token;
                if (company != _settings.Company)
                {
                    var result = await _authService.ObtenerTokenCaptor(company);
                    if (result == null)
                    {
                        _logger.LogWarning("Failed to obtain token for company: {Company}", company);
                        return Json(new { error = "Authentication failed" });
                    }

                    HttpContext.Session.SetString("AuthToken", result.access_token);
                    token = result.access_token;
                }
                else
                {
                    token = HttpContext.Session.GetString("AuthToken");
                }

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(company))
                {
                    _logger.LogWarning("Token or company is empty");
                    return Json(Array.Empty<object>());
                }

                var resp = await getDataQuality.getLinesByCompany(
                    token,
                    _settings.BaseUrl + _settings.QueryLineas + company,
                    company,
                    _settings.trazalog
                );

                var list = resp?.result?.Select(w => new {
                    workplace = w.workplace,
                    workplaceName = w.workplaceName
                }) ?? Enumerable.Empty<object>();

                return Json(list);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerLineas_XR for company: {Company}", company);
                return Json(new { error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerLineas_XR for company: {Company}", company);
                return Json(new { error = "Internal server error" });
            }
        }

        [HttpGet("ObtenerVarY_XR")]
        public async Task<JsonResult> ObtenerVarY_XR(string sku, DateTime startDate, DateTime endDate, string line, string planta)
        {
            try
            {
                var token = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session for ObtenerVarY_XR");
                    return Json(new { value = Array.Empty<object>(), error = "Token not available" });
                }

                var dataTask = getDataQuality.getVarYRows(
                    token,
                    _settings.BaseUrl + _settings.QueryVarY + planta,
                    planta,
                    sku,
                    startDate,
                    endDate,
                    line
                );

                await Task.WhenAll(dataTask);

                var rows = (dataTask.Result?.result ?? Enumerable.Empty<YRawRow>());

                var items = rows
                    .Where(r => !string.IsNullOrWhiteSpace(r.controlOperation))
                    .GroupBy(r => new
                    {
                        Op = (r.controlOperation ?? "").Trim().ToUpper(),
                        Name = (r.controlOperationName ?? "").Trim()
                    })
                    .Select(g => new
                    {
                        value = g.Key.Op,
                        name = string.IsNullOrEmpty(g.Key.Name) ? g.Key.Op : g.Key.Name
                    })
                    .OrderBy(x => x.name)
                    .ToList();

                return Json(new { value = items });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerVarY_XR for sku: {Sku}", sku);
                return Json(new { value = Array.Empty<object>(), error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerVarY_XR for sku: {Sku}", sku);
                return Json(new { value = Array.Empty<object>(), error = "Internal server error" });
            }
        }


        [HttpGet("ObtenerVarX_XR")]
        public async Task<JsonResult> ObtenerVarX_XR(
    string sku, string varY, DateTime fechaInicial, DateTime fechaFinal, string lineaId, string planta)
        {
            try
            {
                var token = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session for ObtenerVarX_XR");
                    return Json(new { value = Array.Empty<object>(), error = "Token not available" });
                }

                var prefix = (varY?.Length >= 3 ? varY.Substring(0, 3) : varY ?? "") + "%";

                var opsTask = getDataQuality.getVarXByvarY(
                    token, _settings.BaseUrl + _settings.QueryVarX + planta, planta,
                    sku, prefix, fechaInicial, fechaFinal, lineaId);

                await Task.WhenAll(opsTask);

                var opsRaw = opsTask.Result?.result ?? new List<result_varY>();

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

                var yName = opsRaw
                    .FirstOrDefault(o => string.Equals(o.controlOperation?.Trim(), varY?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?.controlOperationName;

                var yOption = new
                {
                    value = (varY ?? "").Trim().ToUpper(),
                    name = string.IsNullOrWhiteSpace(yName) ? $"[Y] {varY}" : $"[Y] {yName.Trim()}"
                };

                xs = xs.Where(x => !string.Equals(x.value, yOption.value, StringComparison.OrdinalIgnoreCase))
                       .Prepend(yOption)
                       .ToList();

                return Json(new { value = xs });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerVarX_XR for sku: {Sku}, varY: {VarY}", sku, varY);
                return Json(new { value = Array.Empty<object>(), error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerVarX_XR for sku: {Sku}, varY: {VarY}", sku, varY);
                return Json(new { value = Array.Empty<object>(), error = "Internal server error" });
            }
        }

        // ========================================

        [HttpGet("ObtenerProductos_XR")]
        public async Task<JsonResult> ObtenerProductos_XR(string lineaId, DateTime fechaInicial, DateTime fechaFinal, string planta)
        {
            try
            {
                string token = HttpContext.Session.GetString("AuthToken");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session for ObtenerProductos_XR");
                    return Json(new { error = "Token not available" });
                }

                var dataResultP = getDataQuality.getProductsByLine(
                        token.ToString(),
                        _settings.BaseUrl + _settings.QuerySKUs + planta,
                        planta,
                        lineaId,
                        fechaInicial,
                        fechaFinal);
                await Task.WhenAll(dataResultP);

                var resultado = Json(dataResultP.Result.result);
                return resultado;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerProductos_XR for line: {LineId}", lineaId);
                return Json(new { error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerProductos_XR for line: {LineId}", lineaId);
                return Json(new { error = "Internal server error" });
            }
        }

        // ========================================

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
                _logger.LogError(ex, "Error in GetVariables for workplace: {Workplace}, reference: {Reference}", workplace, reference);
                return Problem("Error retrieving variables.");
            }
        }

        // ========================================

        [HttpGet("base")]
        public IActionResult GetBaseRows(
            DateTime? from,
            DateTime? to,
            string? workplace,
            string? reference,
            string? controlOperation,
            string planta)
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
                _logger.LogError(ex, "Error in GetBaseRows for workplace: {Workplace}, operation: {ControlOperation}", workplace, controlOperation);
                return Problem("Error retrieving base data.");
            }
        }

        // ========================================

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
                _logger.LogError(ex, "Error in GetSubgroupStats for workplace: {Workplace}, operation: {ControlOperation}", workplace, controlOperation);
                return Problem("Error calculating subgroup statistics.");
            }
        }

        // ========================================

        [HttpGet("capability")]
        public IActionResult GetCapability(
            DateTime? from,
            DateTime? to,
            string? workplace,
            string? reference,
            string? controlOperation,
            string planta)
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
                _logger.LogError(ex, "Error in GetCapability for workplace: {Workplace}, operation: {ControlOperation}", workplace, controlOperation);
                return Problem("Error calculating capability.");
            }
        }


    }
}
