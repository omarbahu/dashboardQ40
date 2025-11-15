using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using static dashboardQ40.Models.Models;
using System.Globalization;
using ClosedXML.Excel;

namespace dashboardQ40.Controllers
{
    public class CargaACsController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly IConfiguration _configuration;
        private readonly AuthService _authService;
        private readonly IACPayloadService _acPayloadService;

        public CargaACsController(
            IOptions<WebServiceSettings> settings,
            AuthService authService,
            IConfiguration configuration,
            IACPayloadService acPayloadService)
        {
            _settings = settings.Value;
            _authService = authService;
            _configuration = configuration;
            _acPayloadService = acPayloadService;
        }

        public async Task<IActionResult> ListarACs()
        {
            var token = await _authService.ObtenerTokenCaptor(_settings.Company);
            if (token != null)
            {
                HttpContext.Session.SetString("AuthToken", token.access_token);
            }

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

        public IActionResult CargarACs()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ObtenerRows([FromBody] RowsRequest req)
        {
            try
            {
                string connStr = _configuration.GetConnectionString("CaptorConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                    return BadRequest("Falta ConnectionStrings:CaptorConnection en appsettings.json");

                DateTime? start = null, end = null;
                if (!string.IsNullOrWhiteSpace(req.startdate))
                    start = DateTime.ParseExact(req.startdate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(req.enddate))
                    end = DateTime.ParseExact(req.enddate, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1);

                object DbNullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? (object)DBNull.Value : v;
                object DbNullIfNull(DateTime? d) => d.HasValue ? d.Value : (object)DBNull.Value;

                var parametros = new Dictionary<string, object>
                {
                    { "@company",            DbNullIfEmpty(req.company) },
                    { "@workplace",          DbNullIfEmpty(req.workplace) },
                    { "@manufacturingorder", DbNullIfEmpty(req.manufacturingorder) },
                    { "@startdate",          DbNullIfNull(start) },
                    { "@enddate",            DbNullIfNull(end) }
                };

                var rows = await _acPayloadService.ObtenerRowsAsync(parametros, connStr);

                return Json(new { rows, count = rows?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return BadRequest($"ObtenerRows exception: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportRows([FromBody] RowsRequest req)
        {
            try
            {
                string connStr = _configuration.GetConnectionString("CaptorConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                    return BadRequest("Falta ConnectionStrings:CaptorConnection.");

                DateTime? start = null, end = null;
                if (!string.IsNullOrWhiteSpace(req.startdate))
                    start = DateTime.ParseExact(req.startdate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(req.enddate))
                    end = DateTime.ParseExact(req.enddate, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1);

                object DbNullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? (object)DBNull.Value : v;
                object DbNullIfNull(DateTime? d) => d.HasValue ? d.Value : (object)DBNull.Value;

                var parametros = new Dictionary<string, object>
                {
                    { "@company",            DbNullIfEmpty(req.company) },
                    { "@workplace",          DbNullIfEmpty(req.workplace) },
                    { "@manufacturingorder", DbNullIfEmpty(req.manufacturingorder) },
                    { "@startdate",          DbNullIfNull(start) },
                    { "@enddate",            DbNullIfNull(end) }
                };

                var rows = await _acPayloadService.ObtenerRowsAsync(parametros, connStr)
                           ?? new List<ACPayloadRow>();

                var colOrder = _configuration
                    .GetSection("ExcelToSqlMappings:ACPayloadRows:ColumnMappings")
                    .GetChildren()
                    .Select(c => c.Key)
                    .ToList();

                using var wb = new XLWorkbook();
                var ws = wb.AddWorksheet("Autocontroles");

                for (int i = 0; i < colOrder.Count; i++)
                    ws.Cell(1, i + 1).Value = colOrder[i];

                int r = 2;
                var props = typeof(ACPayloadRow).GetProperties();
                foreach (var row in rows)
                {
                    for (int c = 0; c < colOrder.Count; c++)
                    {
                        var prop = props.FirstOrDefault(p => p.Name == colOrder[c]);
                        var val = prop?.GetValue(row);
                        ws.Cell(r, c + 1).SetValue(val?.ToString() ?? "");
                    }
                    r++;
                }

                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                var bytes = ms.ToArray();

                var fileName = $"Autocontroles_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"ExportRows exception: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportarExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No se recibió archivo");

            // 1) Leer Excel
            List<AutocontrolExcelRow> filas;
            using (var stream = file.OpenReadStream())
            {
                filas = AutocontrolExcelReader.LeerDesdeExcel(stream);
            }

            // 2) Construir payloads: 1 JSON por IdControlProcedureResult
            var payloads = AutocontrolPayloadBuilder.BuildOnePerControlProcedureResult(filas);

            // 3) Config de SOAP
            var captorCfg = _configuration.GetSection("CaptorAutocontrol");
            string soapUrl = captorCfg.GetValue<string>("PerformActionsUrl");
            string soapNs = captorCfg.GetValue<string>("SoapNamespace");
            string soapAct = captorCfg.GetValue<string>("SoapActionCompleteControlProcedure");
            string user = captorCfg.GetValue<string>("User");
            string company = _settings.Company;

            int enviadosOk = 0;
            int enviadosError = 0;
            var errores = new List<string>();

            // 4) SOLO SOAP: enviar un payload por CPR
            foreach (var payload in payloads)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(payload);

                    var respXml = await CaptorPerformActionsService.CompleteControlProcedureAsync(
                        soapUrl,
                        soapNs,
                        soapAct,
                        company,
                        user,
                        json
                    );

                    enviadosOk++;
                }
                catch (Exception ex)
                {
                    enviadosError++;
                    errores.Add($"[SOAP] CPR {payload.IdControlProcedureResult}: {ex.Message}");
                }
            }

            return Json(new
            {
                totalFilas = filas.Count,
                totalPayloads = payloads.Count,
                enviadosOk,
                enviadosError,
                primerosErrores = errores.Take(5),
                ejemploJson = payloads.Any()
                    ? JsonConvert.SerializeObject(payloads.First(), Formatting.Indented)
                    : null
            });
        }
    }
}
