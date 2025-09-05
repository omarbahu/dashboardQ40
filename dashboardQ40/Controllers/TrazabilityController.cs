using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;
using static dashboardQ40.Models.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

using System.Data.SqlClient;
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


            return View();
        }


        public async Task<IActionResult> ReportTrazability(string searchBatch, DateTime? startDate, DateTime? endDate, string planta)
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


            if (!string.IsNullOrEmpty(searchBatch))
            {
                string connStr = _configuration.GetConnectionString("CaptorConnection");
                string company = planta;
                //string company = _configuration.GetConnectionString("company");

                BatchInfo batchInfo = TrazabilityClass.GetBatchInfoByText(searchBatch, connStr, company);
                long batch = batchInfo.BatchId;

                // 👇 Asigna las fechas reales si no vinieron del querystring
                if (!startDate.HasValue)
                    model.StartDate = batchInfo.StartDate;

                if (!endDate.HasValue)
                    model.EndDate = batchInfo.EndDate;

                if (batch > 0)
                {
                    DataTable trazabilidad = TrazabilityClass.GetBackwardTraceability(company, batch, connStr);
                    DataTable checklist = TrazabilityClass.GetChecklistByTraceability(trazabilidad, company, connStr);

                    // 🔍 Filtrar si se especificó fecha manual
                    if (startDate.HasValue && endDate.HasValue)
                    {
                        if (startDate >= endDate)
                        {
                            ViewBag.ErrorMessage = "La fecha de fin debe ser mayor a la fecha de inicio.";
                            return View(model);
                        }

                        var filtradas = checklist.AsEnumerable()
                            .Where(row =>
                                !row.IsNull("executionDate") && // 🛡️ Evita el error
                                row.Field<DateTime>("executionDate") >= startDate.Value &&
                                row.Field<DateTime>("executionDate") <= endDate.Value);

                        checklist = filtradas.Any() ? filtradas.CopyToDataTable() : checklist.Clone();
                    }

                    var estadisticas = TrazabilityStats.CalcularEstadisticas(checklist);
                    var statsDict = estadisticas.ToDictionary(e => $"{e.Lote}|{e.Variable}", e => e);

                    model.Checklist = checklist;
                    model.Trazabilidad = trazabilidad;
                    model.Estadisticas = statsDict;
                }
                else
                {
                    ViewBag.ErrorMessage = "No se encontró ningún lote con ese texto.";
                }
            }


            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> BuscarLotesPorFechas(DateTime fechaIni, DateTime fechaFin, string planta)
        {
            // Normaliza fin al final del día si quieres incluir todo el día
            var fin = fechaFin.Date.AddDays(1).AddTicks(-1);

            string connStr = _configuration.GetConnectionString("CaptorConnection");
            string company = planta;

            DataTable batchs = TrazabilityClass.GetBatchbyDates(company, fechaIni, fin, connStr);
            // TODO: reemplaza por tu servicio/consulta real
            // Debe devolver: lote, descripcion (o SKU), inicio, fin
            // Si no hay filas, regresa lista vacía
            if (batchs == null || batchs.Rows.Count == 0)
                return new JsonResult(Array.Empty<object>());

            // Mapea DataTable -> JSON-friendly
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

            return new JsonResult(lista);
        }


        private static DateTime? GetNullableDate(DataRow row, string colName)
        {
            if (!row.Table.Columns.Contains(colName)) return null;
            var val = row[colName];
            if (val == DBNull.Value || val == null) return null;

            if (val is DateTime dt) return dt;

            // A veces vienen como string ISO/SQL
            if (DateTime.TryParse(val.ToString(), out var parsed)) return parsed;

            return null;
        }
    }


}
