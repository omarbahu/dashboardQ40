using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;
using static dashboardQ40.Models.AnalisisSensorialesModel;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Controllers
{
    public class AnalisisSensorialController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;

        private readonly ILogger<DashboardController> _logger;
        private readonly IConfiguration _configuration;
        public AnalisisSensorialController(IOptions<WebServiceSettings> settings, AuthService authService, IConfiguration configuration, ILogger<DashboardController> logger)
        {
            _settings = settings.Value;
            _authService = authService;
            _logger = logger;
            _configuration = configuration;

          

        }
    
        public async Task<IActionResult> Index()
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
                }

                return View();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in AnalisisSensorial Index");
                ViewBag.ErrorMessage = "Unable to load company data. Please try again.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AnalisisSensorial Index");
                ViewBag.ErrorMessage = "An unexpected error occurred.";
                return View();
            }
        }

        // ========================================

        [HttpPost]
        public async Task<IActionResult> GetReporteAnaSens([FromBody] AnalisisSensorialRequest req)
        {
            try
            {
                // Validación de entrada
                if (req == null || string.IsNullOrWhiteSpace(req.startDate) ||
                    string.IsNullOrWhiteSpace(req.endDate) ||
                    string.IsNullOrWhiteSpace(req.company))
                {
                    _logger.LogWarning("Missing parameters in GetReporteAnaSens");
                    return BadRequest("Missing parameters.");
                }

                if (!DateTime.TryParse(req.startDate, out var from))
                {
                    _logger.LogWarning("Invalid start date: {StartDate}", req.startDate);
                    return BadRequest("Invalid start date.");
                }

                if (!DateTime.TryParse(req.endDate, out var to))
                {
                    _logger.LogWarning("Invalid end date: {EndDate}", req.endDate);
                    return BadRequest("Invalid end date.");
                }

                // Swap si están invertidas
                if (to < from)
                {
                    var tmp = from;
                    from = to;
                    to = tmp;
                }

                var company = req.company;

                // Token
                var tokenStr = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(tokenStr))
                {
                    var tokenObj = await _authService.ObtenerTokenCaptor(company);
                    if (tokenObj == null)
                    {
                        _logger.LogWarning("Failed to obtain token for company: {Company}", company);
                        return BadRequest("Could not obtain authentication token.");
                    }
                    tokenStr = tokenObj.access_token;
                    HttpContext.Session.SetString("AuthToken", tokenStr);
                }

                string trazalog = _settings.trazalog;

                // 1) Query de lotes
                var loteResult = await AnalisisSensorialService.getLoteAnaSens(
                    tokenStr,
                    _settings.BaseUrl + _settings.QueryanasensDEL + _settings.Company,
                    company,
                    trazalog,
                    from,
                    to);

                var loteRows = loteResult?.result ?? new List<BatchExtraRowDto>();
                if (!loteRows.Any())
                {
                    _logger.LogInformation("No batches found for date range: {From} to {To}", from, to);
                    return Json(new { success = true, rows = new List<ReporteSensorialFila>() });
                }

                int batch = loteRows.First().Batch;

                // 2) Query de ACs
                var acsResult = await AnalisisSensorialService.getACsAnaSens(
                    tokenStr,
                    _settings.BaseUrl + _settings.QueryanasensACs + _settings.Company,
                    company,
                    trazalog,
                    batch);

                var acRows = acsResult?.result ?? new List<AutoControlRowDto>();

                // 3) Generar reporte
                var filas = AnalisisSensorialService.BuildReporteSensorial(loteRows, acRows);

                _logger.LogInformation("Sensorial report generated successfully for batch: {Batch}, rows: {Count}", batch, filas.Count);

                return Json(new
                {
                    success = true,
                    batch = batch,
                    total = filas.Count,
                    rows = filas
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in GetReporteAnaSens");
                return StatusCode(503, "External service unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetReporteAnaSens");
                return StatusCode(500, "Internal error while generating report.");
            }
        }

        // ========================================

        [HttpPost]
        public async Task<IActionResult> GetReporteAnaSensByProduct([FromBody] AnalisisSensorialRequest req)
        {
            try
            {
                // Validación
                if (req == null ||
                    string.IsNullOrWhiteSpace(req.company) ||
                    string.IsNullOrWhiteSpace(req.productCode))
                {
                    _logger.LogWarning("Missing parameters in GetReporteAnaSensByProduct");
                    return BadRequest("Missing parameters (company / productCode).");
                }

                var company = req.company;
                var productCode = req.productCode;
                var productHour = req.productHour;

                // Token
                var tokenStr = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(tokenStr))
                {
                    var tokenObj = await _authService.ObtenerTokenCaptor(company);
                    if (tokenObj == null)
                    {
                        _logger.LogWarning("Failed to obtain token for company: {Company}", company);
                        return BadRequest("Could not obtain authentication token.");
                    }
                    tokenStr = tokenObj.access_token;
                    HttpContext.Session.SetString("AuthToken", tokenStr);
                }

                string trazalog = _settings.trazalog;

                // 1) Query de lotes por código de producto
                var loteResult = await AnalisisSensorialService.getLoteAnaSensbyCode(
                    tokenStr,
                    _settings.BaseUrl + _settings.QueryanasensDELbycode + _settings.Company,
                    company,
                    trazalog,
                    productCode,
                    productHour);

                var loteRows = loteResult?.result ?? new List<BatchExtraRowDto>();
                if (!loteRows.Any())
                {
                    _logger.LogInformation("No batches found for productCode: {ProductCode}", productCode);
                    return Json(new { success = true, rows = new List<ReporteSensorialFila>() });
                }

                // Agrupamos por batch (puede haber múltiples lotes)
                var gruposPorBatch = loteRows
                    .GroupBy(r => r.Batch)
                    .OrderByDescending(g => g.Max(r => r.StartDate))
                    .ToList();

                // Lote más reciente
                var loteSeleccionado = gruposPorBatch.First();
                int batch = loteSeleccionado.Key;
                loteRows = loteSeleccionado.ToList();

                // 2) ACs del lote
                var acsResult = await AnalisisSensorialService.getACsAnaSens(
                    tokenStr,
                    _settings.BaseUrl + _settings.QueryanasensACs + _settings.Company,
                    company,
                    trazalog,
                    batch);

                var acRows = acsResult?.result ?? new List<AutoControlRowDto>();

                // Filtrar ACs por números de muestra válidos
                var extrasPorNumero = AnalisisSensorialService.MapLoteExtras(loteRows);
                var numerosValidos = extrasPorNumero.Keys.ToHashSet();

                var acRowsFiltrados = acRows
                    .Where(a =>
                    {
                        var n = AnalisisSensorialService.TryGetNumeroFromOpName(a.ControlOperationName);
                        return n.HasValue && numerosValidos.Contains(n.Value);
                    })
                    .ToList();

                // 3) Generar reporte
                var filas = AnalisisSensorialService.BuildReporteSensorial(loteRows, acRowsFiltrados);

                _logger.LogInformation("Sensorial report by product generated successfully. Batch: {Batch}, ProductCode: {ProductCode}, Rows: {Count}",
                    batch, productCode, filas.Count);

                return Json(new
                {
                    success = true,
                    batch = batch,
                    total = filas.Count,
                    rows = filas
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in GetReporteAnaSensByProduct for productCode: {ProductCode}", req?.productCode);
                return StatusCode(503, "External service unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetReporteAnaSensByProduct for productCode: {ProductCode}", req?.productCode);
                return StatusCode(500, "Internal error while generating report by product code.");
            }
        }

    }
}
