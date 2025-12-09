using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;
using static dashboardQ40.Models.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

using Microsoft.Data.SqlClient;
using System.Linq;
using System.Globalization;
using Microsoft.Extensions.Options;                         // Linq



namespace dashboardQ40.Controllers
{
    public class TrazabilityController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;
        private readonly ILogger<DashboardController> _logger;
        private readonly IConfiguration _configuration;


        public TrazabilityController(IOptions<WebServiceSettings> settings,
            AuthService authService,
            IConfiguration configuration,
            ILogger<DashboardController> logger)
        {
            _settings = settings.Value;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }
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

                return View();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in TrazabilityController IndexAsync");
                ViewBag.ErrorMessage = "Unable to load company data. Please try again.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TrazabilityController IndexAsync");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please contact support.";
                return View();
            }
        }


        public async Task<IActionResult> ReportTrazability(string searchBatch, DateTime? startDate, DateTime? endDate, string planta)
        {
            try
            {
                var model = new TrazabilidadConChecklistViewModel
                {
                    SearchBatch = searchBatch,
                    StartDate = startDate,
                    EndDate = endDate
                };

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

                if (!string.IsNullOrEmpty(searchBatch))
                {
                    string connStr = _configuration.GetConnectionString("CaptorConnection");
                    string company = planta;

                    BatchInfo batchInfo = TrazabilityClass.GetBatchInfoByText(searchBatch, connStr, company);

                    if (batchInfo == null)
                    {
                        _logger.LogWarning("Batch not found: {SearchBatch}", searchBatch);
                        ViewBag.ErrorMessage = "Batch not found with that text.";
                        return View(model);
                    }

                    long batch = batchInfo.BatchId;

                    if (!startDate.HasValue)
                        model.StartDate = batchInfo.StartDate;

                    if (!endDate.HasValue)
                        model.EndDate = batchInfo.EndDate;

                    if (batch > 0)
                    {
                        DataTable trazabilidad = TrazabilityClass.GetBackwardTraceability(company, batch, connStr);
                        DataTable checklist = TrazabilityClass.GetChecklistByTraceability(trazabilidad, company, connStr);

                        if (startDate.HasValue && endDate.HasValue)
                        {
                            if (startDate >= endDate)
                            {
                                ViewBag.ErrorMessage = "End date must be greater than start date.";
                                return View(model);
                            }

                            var filtradas = checklist.AsEnumerable()
                                .Where(row =>
                                    !row.IsNull("executionDate") &&
                                    row.Field<DateTime>("executionDate") >= startDate.Value &&
                                    row.Field<DateTime>("executionDate") <= endDate.Value);

                            checklist = filtradas.Any() ? filtradas.CopyToDataTable() : checklist.Clone();
                        }

                        var estadisticas = TrazabilityStats.CalcularEstadisticas(checklist, trazabilidad);
                        ViewBag.DebugStats = estadisticas
                            .Select(e => $"{e.Lote} | {e.Variable} | N={e.Conteo} | Media={e.Media:0.00} | Cpk={e.Cpk:0.00}")
                            .ToList();

                        var statsDict = estadisticas.ToDictionary(
                            e => $"{e.Lote}|{e.Variable}",
                            e => e
                        );

                        model.Checklist = checklist;
                        model.Trazabilidad = trazabilidad;
                        model.Estadisticas = statsDict;

                        _logger.LogInformation("Traceability report generated successfully for batch: {SearchBatch}", searchBatch);
                    }
                    else
                    {
                        _logger.LogWarning("No batch ID found for search: {SearchBatch}", searchBatch);
                        ViewBag.ErrorMessage = "No batch found with that text.";
                    }
                }

                return View(model);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database error in ReportTrazability for batch: {SearchBatch}", searchBatch);
                ViewBag.ErrorMessage = "Database error occurred. Please try again later.";
                return View(new TrazabilidadConChecklistViewModel { SearchBatch = searchBatch });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ReportTrazability for batch: {SearchBatch}", searchBatch);
                ViewBag.ErrorMessage = "External service unavailable. Please try again.";
                return View(new TrazabilidadConChecklistViewModel { SearchBatch = searchBatch });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ReportTrazability for batch: {SearchBatch}", searchBatch);
                ViewBag.ErrorMessage = "An unexpected error occurred while generating the report.";
                return View(new TrazabilidadConChecklistViewModel { SearchBatch = searchBatch });
            }
        }


        [HttpGet]
        public async Task<IActionResult> BuscarLotesPorFechas(DateTime fechaIni, DateTime fechaFin, string planta)
        {
            try
            {
                // Normaliza fin al final del día
                var fin = fechaFin.Date.AddDays(1).AddTicks(-1);

                string connStr = _configuration.GetConnectionString("CaptorConnection");
                string company = planta;

                DataTable batchs = TrazabilityClass.GetBatchbyDates(company, fechaIni, fin, connStr);

                if (batchs == null || batchs.Rows.Count == 0)
                {
                    _logger.LogInformation("No batches found for dates: {FechaIni} to {FechaFin}", fechaIni, fechaFin);
                    return new JsonResult(Array.Empty<object>());
                }

                var lista = batchs.AsEnumerable().Select(r => new
                {
                    lote = r.Table.Columns.Contains("batchIdentifier")
                                    ? (r["batchIdentifier"]?.ToString() ?? "")
                                    : (r.Table.Columns.Contains("batch") ? r["batch"]?.ToString() ?? "" : ""),
                    descripcion = r.Table.Columns.Contains("manufacturingReferenceName")
                                    ? (r["manufacturingReferenceName"]?.ToString() ?? "")
                                    : "",
                    inicio = GetNullableDate(r, "startDate"),
                    fin = GetNullableDate(r, "endDate")
                }).ToList();

                _logger.LogInformation("Found {Count} batches for dates: {FechaIni} to {FechaFin}", lista.Count, fechaIni, fechaFin);
                return new JsonResult(lista);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Database error in BuscarLotesPorFechas for dates: {FechaIni} to {FechaFin}", fechaIni, fechaFin);
                return StatusCode(500, new { error = "Database error occurred", details = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in BuscarLotesPorFechas for dates: {FechaIni} to {FechaFin}", fechaIni, fechaFin);
                return StatusCode(500, new { error = "An unexpected error occurred", details = ex.Message });
            }
        }


        private static DateTime? GetNullableDate(DataRow row, string colName)
        {
            if (!row.Table.Columns.Contains(colName)) return null;
            var val = row[colName];
            if (val == DBNull.Value || val == null) return null;

            if (val is DateTime dt) return dt;

            if (DateTime.TryParse(val.ToString(), out var parsed)) return parsed;

            return null;
        }
    }


}
