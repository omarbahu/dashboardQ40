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


                var countries = companies
                       .GroupBy(c => c.CountryCode)
                       .Select(g =>
                       {
                           var r = new RegionInfo(g.Key); // admite "MX","US","ES"
                           return new { Code = g.Key, Name = r.NativeName }; // "México", "Estados Unidos"
                       })
                       .OrderBy(x => x.Name)
                       .ToList();

                ViewBag.Companies = companies;                 // lista completa
                ViewBag.Countries = countries;                 // países únicos
                ViewBag.CompaniesJson = JsonConvert.SerializeObject(companies);


                ViewBag.produccion = _settings.Produccion;

            }

            return View();
        }


        [HttpPost]
        public async Task<IActionResult> GetReporteAnaSens([FromBody] AnalisisSensorialRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.startDate) ||
                string.IsNullOrWhiteSpace(req.endDate) ||
                string.IsNullOrWhiteSpace(req.company))
            {
                return BadRequest("Faltan parámetros.");
            }

            if (!DateTime.TryParse(req.startDate, out var from))
                return BadRequest("Fecha inicial inválida.");

            if (!DateTime.TryParse(req.endDate, out var to))
                return BadRequest("Fecha final inválida.");

            // Por si quieres asegurar que to > from
            if (to < from)
            {
                var tmp = from;
                from = to;
                to = tmp;
            }

            var company = req.company;

            try
            {
                // Token (usamos el de sesión si existe, si no pedimos uno nuevo)
                var tokenStr = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(tokenStr))
                {
                    var tokenObj = await _authService.ObtenerTokenCaptor(company);
                    if (tokenObj == null)
                        return BadRequest("No se pudo obtener token de Captor.");
                    tokenStr = tokenObj.access_token;
                    HttpContext.Session.SetString("AuthToken", tokenStr);
                }

                string trazalog = _settings.trazalog;

                
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
                    return Json(new { success = true, rows = new List<ReporteSensorialFila>() });
                }

                // Tomamos el Batch de la primera fila (asumimos que el query ya viene filtrado a un lote)
                int batch = loteRows.First().Batch;

                // 2) Segundo query: ACs (CProcResultWithValuesStatus) por batch
                var acsResult = await AnalisisSensorialService.getACsAnaSens(
                    tokenStr,
                    _settings.BaseUrl + _settings.QueryanasensACs + _settings.Company,
                    company,
                    trazalog,
                    batch);

                var acRows = acsResult?.result ?? new List<AutoControlRowDto>();

                // 3) Mezclamos ambos para armar el reporte por fila
                var filas = AnalisisSensorialService.BuildReporteSensorial(loteRows, acRows);

                var retorno = Json(new
                {
                    success = true,
                    batch = batch,
                    total = filas.Count,
                    rows = filas
                });
                return retorno;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetReporteAnaSens");
                return StatusCode(500, "Error interno al generar el reporte.");
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetReporteAnaSensByProduct([FromBody] AnalisisSensorialRequest req)
        {
            if (req == null ||
                string.IsNullOrWhiteSpace(req.company) ||
                string.IsNullOrWhiteSpace(req.productCode))
            {
                return BadRequest("Faltan parámetros (company / productCode).");
            }

            var company = req.company;
            var productCode = req.productCode;
            var productHour = req.productHour;
            try
            {
                // Token (igual que en GetReporteAnaSens)
                var tokenStr = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(tokenStr))
                {
                    var tokenObj = await _authService.ObtenerTokenCaptor(company);
                    if (tokenObj == null)
                        return BadRequest("No se pudo obtener token de Captor.");
                    tokenStr = tokenObj.access_token;
                    HttpContext.Session.SetString("AuthToken", tokenStr);
                }

                string trazalog = _settings.trazalog;

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
                    return Json(new { success = true, rows = new List<ReporteSensorialFila>() });
                }

                // 🔹 Agrupamos por batch porque puede haber más de un lote que cumpla código+hora
                var gruposPorBatch = loteRows
                .GroupBy(r => r.Batch)
                // Ordenamos por la fecha de inicio más reciente del grupo
                .OrderByDescending(g => g.Max(r => r.StartDate))
                .ToList();

                // 🔹 Nos quedamos con el lote "más reciente"
                var loteSeleccionado = gruposPorBatch.First();
                int batch = loteSeleccionado.Key;
                loteRows = loteSeleccionado.ToList();

                // ACs de TODO ese lote (como antes)
                var acsResult = await AnalisisSensorialService.getACsAnaSens(
                    tokenStr,
                    _settings.BaseUrl + _settings.QueryanasensACs + _settings.Company,
                    company,
                    trazalog,
                    batch);

                var acRows = acsResult?.result ?? new List<AutoControlRowDto>();

                // 1) Números de muestra que tenemos en EXTRAS (ya vienen filtrados por código/hora)
                var extrasPorNumero = AnalisisSensorialService.MapLoteExtras(loteRows);
                var numerosValidos = extrasPorNumero.Keys.ToHashSet();

                // 2) Filtrar ACs SOLO a esos números
                var acRowsFiltrados = acRows
                    .Where(a =>
                    {
                        var n = AnalisisSensorialService.TryGetNumeroFromOpName(a.ControlOperationName);
                        return n.HasValue && numerosValidos.Contains(n.Value);
                    })
                    .ToList();

                // 3) Mezclar
                var filas = AnalisisSensorialService.BuildReporteSensorial(loteRows, acRowsFiltrados);

                return Json(new
                {
                    success = true,
                    batch = batch,
                    total = filas.Count,
                    rows = filas
                });



            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetReporteAnaSensByProduct");
                return StatusCode(500, "Error interno al generar el reporte por código de producto.");
            }
        }

    }
}
