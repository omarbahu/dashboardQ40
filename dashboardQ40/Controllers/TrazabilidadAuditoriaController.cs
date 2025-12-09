using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Globalization;
using static dashboardQ40.Models.Models;
using static dashboardQ40.Services.AuditTrazabilityClass;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace dashboardQ40.Controllers
{
    public class TrazabilidadAuditoriaController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;
        private readonly ILogger<DashboardController> _logger;
        private readonly IConfiguration _configuration;
        public TrazabilidadAuditoriaController(IOptions<WebServiceSettings> settings,
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
                _logger.LogError(ex, "HTTP request failed in TrazabilidadAuditoria IndexAsync");
                ViewBag.ErrorMessage = "Unable to load company data. Please try again.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in TrazabilidadAuditoria IndexAsync");
                ViewBag.ErrorMessage = "An unexpected error occurred. Please contact support.";
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarReporte([FromBody] ReporteTrazabilidad modelo)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errores = ModelState
                        .Where(kvp => kvp.Value.Errors.Any())
                        .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value.Errors.Select(e => e.ErrorMessage))}")
                        .ToList();

                    _logger.LogWarning("Invalid model state in GuardarReporte: {Errors}", string.Join("; ", errores));
                    return BadRequest(new { success = false, mensaje = "Invalid data", detalles = errores });
                }

                if (modelo.simulate)
                {
                    return Ok(new { success = true, simulated = true, mensaje = "Simulation completed. No data saved to database." });
                }

                if (string.IsNullOrWhiteSpace(modelo.company))
                {
                    _logger.LogWarning("Missing required field: company");
                    return BadRequest(new { success = false, mensaje = "Plant is required." });
                }

                var cs = _configuration.GetConnectionString("ArcaTrazability");
                var id = await AuditTrazabilityClass.GuardarReporteAsync(modelo, cs);

                _logger.LogInformation("Report saved successfully with ID: {ReportId}", id);
                return Ok(new { success = true, simulated = false, id, mensaje = "Report saved successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report in GuardarReporte");
                return StatusCode(500, new { success = false, mensaje = "Error saving report. Please try again." });
            }
        }




        public async Task<IActionResult> AuditReport(string lote, TimeSpan horaQueja, string company)
        {
            try
            {
                var token = await _authService.ObtenerTokenCaptor(_settings.Company);

                if (string.IsNullOrWhiteSpace(lote))
                {
                    _logger.LogWarning("AuditReport called with empty lote");
                    return View();
                }

                string connStr = _configuration.GetConnectionString("CaptorConnection");

                var batchInfo = ObtenerBatchDesdeLote(lote, connStr, company);
                if (batchInfo == null || batchInfo.BatchId <= 0)
                {
                    _logger.LogWarning("Batch not found for lote: {Lote}", lote);
                    ViewBag.Mensaje = $"Batch '{lote}' was not found or has no information.";
                    return View("LoteNoEncontrado");
                }

                var trazabilidadNodos = ObtenerTrazabilidadCompleta(company, batchInfo.BatchId, connStr);
                var bloqueMateriaPrima = ObtenerBloqueMateriaPrima(trazabilidadNodos, company, connStr);
                var bloqueProdTerm = ObtenerBloqueProductoTerminado(batchInfo.BatchId, company, connStr);

                var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchInfo.BatchId);
                DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
                DateTime fechaHoraQueja = fechaProduccion + horaQueja;

                var nodosJarabeTerminado = trazabilidadNodos
                   .Where(n => n.ManufacturingReferenceName.ToUpper().Contains("JARABE TERMINADO"))
                   .OrderBy(n => n.StartDate)
                   .ToList();

                var jarabeterminadoActivo = nodosJarabeTerminado
                    .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

                var nodosJarabeSimple = trazabilidadNodos
                    .Where(n => n.ManufacturingReferenceName.ToUpper().Contains("JARABE SIMPLE"))
                    .OrderBy(n => n.StartDate)
                    .ToList();

                var jarabeSimpleActivo = nodosJarabeSimple
                    .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

                var batchInfoJT = new BatchInfo();
                var batchInfoJS = new BatchInfo();

                if (jarabeterminadoActivo != null)
                {
                    batchInfoJT = ObtenerBatchDesdeLote(jarabeterminadoActivo.BatchIdentifier, connStr, company);
                }
                if (jarabeSimpleActivo != null)
                {
                    batchInfoJS = ObtenerBatchDesdeLote(jarabeSimpleActivo.BatchIdentifier, connStr, company);
                }

                var bloqueLiberacion = await AuditTrazabilityClass.ObtenerPruebasLiberacionJarabeTerminadoAsync(
                    batchInfo.StartDate, batchInfo.EndDate, batchInfo.workplace, company,
                    batchInfo.manufacturingReference, connStr);

                var registrotiempo = AuditTrazabilityClass.ObtenerEntregaInformacion(lote);

                (BloqueLotesPrincipalesModel LoteJarabeSimple, TrazabilidadNode? jarabeSimpleActivo2) =
                    AuditTrazabilityClass.ObtenerJarabeSimpleConContacto(trazabilidadNodos, batchInfo.BatchId, horaQueja);

                var AnalisisFisicoquimicoJarabeSimple = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F002", "ANÁLISIS FISICOQUÍMICOS DE JARABE SIMPLE", "AnalisisFisicoquimico");

                var AnalisisSensorialJarabeSimple = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeSimple(lote);
                var JarabeTerminado = AuditTrazabilityClass.ObtenerJarabeTerminado(trazabilidadNodos, batchInfo.BatchId, horaQueja, batchInfoJT);

                var AnalisisFisicoquimicoJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F003", "ANÁLISIS FISICOQUÍMICOS DE JARABE TERMINADO", "AnalisisFisicoquimico");

                var AnalisisSensorialJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeTerminado(lote);

                // BLOQUE MATERIAS PRIMAS
                var Azucar = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F001", "AZÚCAR", "AnalisisFisicoquimico");

                var fructuosaSILO1 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F007", "FRUCTOSA SILO 1", "AnalisisFisicoquimico");

                var co2 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F013", "CO2", "AnalisisFisicoquimico");

                var NITROGENO = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F016", "NITROGENO", "AnalisisFisicoquimico");

                var fructuosaSILO2 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F007", "FRUCTOSA SILO 2", "AnalisisFisicoquimico");

                var fructuosaSILO3 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F007", "FRUCTOSA SILO 3", "AnalisisFisicoquimico");

                // BLOQUE TRATAMIENTO DE AGUA
                var aguatratada = AuditTrazabilityClass.ObtenerTratamientodeAgua(trazabilidadNodos, batchInfo.BatchId, horaQueja, batchInfoJT);

                var aguatratadajarabesimple = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoBydate(
                    trazabilidadNodos, batchInfoJS.BatchId, horaQueja, company, connStr, _configuration,
                    "F014", "AGUA TRATADA UTILIZADA EN JARABE SIMPLE", "AnalisisFisicoquimicoBydate");

                var aguatratadajarabeterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoBydate(
                    trazabilidadNodos, batchInfoJT.BatchId, horaQueja, company, connStr, _configuration,
                    "F014", "AGUA TRATADA UTILIZADA EN JARABE TERMINADO", "AnalisisFisicoquimicoBydate");

                var aguatratadaproductoterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoBydate(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F014", "AGUA TRATADA UTILIZADA EN PRODUCTO TERMINADO", "AnalisisFisicoquimicoBydate");

                var aguatratadacruda = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoBydate(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F017", "AGUA CRUDA, TRATADA CLORADA,SUAVIZADA O RECUPERADA, PARA EL ENJUAGUE DEL ENVASE (EN CASO DE QUE APLIQUE)",
                    "AnalisisFisicoquimicoBydate");

                var aguatratadasuave = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoBydate(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F018", "AGUA SUAVE UTILIZADA", "AnalisisFisicoquimicoBydate");

                // BLOQUE LIMPIEZA Y SANEAMIENTO
                var saneo = AuditTrazabilityClass.ObtenerSaneo(trazabilidadNodos, batchInfo.BatchId, horaQueja, batchInfoJT);

                var saneojarabesimple = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfoJS.BatchId, horaQueja, company, connStr, _configuration,
                    "F015", "SANEO UTILIZADA EN JARABE SIMPLE", "AnalisisFisicoquimico");

                var saneojarabeterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfoJT.BatchId, horaQueja, company, connStr, _configuration,
                    "F015", "SANEO UTILIZADA EN JARABE TERMINADO", "AnalisisFisicoquimico");

                var asaneoproductoterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(
                    trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration,
                    "F015", "SANEO UTILIZADA EN PRODUCTO TERMINADO", "AnalisisFisicoquimico");

                var modelo = new FormatoViewModel
                {
                    Lote = lote,
                    BloqueMateriaPrima = bloqueMateriaPrima,
                    BloqueProductoTerminado = bloqueProdTerm,
                    BloqueRegistroTiempo = registrotiempo,
                    BloqueJarabesLoteJarabeSimpleContacto = LoteJarabeSimple,
                    BloqueAnalisisFisicoquimicoJarabeSimple = AnalisisFisicoquimicoJarabeSimple,
                    BloqueAnalisisSensorialJarabeSimple = AnalisisSensorialJarabeSimple,
                    BloqueJarabeTerminado = JarabeTerminado,
                    BloqueAnalisisFisicoquimicoJarabeTerminado = AnalisisFisicoquimicoJarabeTerminado,
                    BloqueAnalisisSensorialJarabeTerminado = AnalisisSensorialJarabeTerminado,
                    BloquePruebasLiberacion = bloqueLiberacion,
                    BloqueAzucar = Azucar,
                    BloqueFructuosa1 = fructuosaSILO1,
                    BloqueCO2 = co2,
                    BloqueAguaTratada = aguatratada,
                    BloqueFructuosa2 = fructuosaSILO2,
                    BloqueFructuosa3 = fructuosaSILO3,
                    Bloquenitrogeno = NITROGENO,
                    Bloqueaguatratadajarabesimple = aguatratadajarabesimple,
                    Bloqueaguatratadajarabeterminado = aguatratadajarabeterminado,
                    Bloqueaguatratadaproductoterminado = aguatratadaproductoterminado,
                    Bloqueaguatratadacruda = aguatratadacruda,
                    Bloqueaguatratadasuave = aguatratadasuave,
                    Bloquesaneo = saneo,
                    Bloquesaneojarabesimple = saneojarabesimple,
                    Bloquesaneojarabeterminado = saneojarabeterminado,
                    Bloqueasaneoproductoterminado = asaneoproductoterminado
                };

                _logger.LogInformation("Audit report generated successfully for lote: {Lote}", lote);
                return View(modelo);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in AuditReport for lote: {Lote}", lote);
                ViewBag.ErrorMessage = "Unable to retrieve audit data. External service unavailable.";
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in AuditReport for lote: {Lote}", lote);
                ViewBag.ErrorMessage = "An unexpected error occurred while generating the audit report.";
                return View("Error");
            }
        }

        public IActionResult HistorialPartial()
        {
            return PartialView("_Historial");
        }

        [HttpGet]
        public IActionResult GetHistorial(string q = "", string company = "")
        {
            try
            {
                string connStr = _configuration.GetConnectionString("ArcaTrazability");
                var items = AuditHistorialService.ObtenerHistorial(_configuration, connStr, "HistorialReportes", q, company);

                _logger.LogInformation("History retrieved: {Count} items", items.Count);
                return Json(new { items, total = items.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving history with query: {Query}, company: {Company}", q, company);
                return Json(new { items = new List<object>(), total = 0, error = "Error retrieving history" });
            }
        }

        [AcceptVerbs("GET", "POST")]
        public IActionResult AuditarDesdeHistorial(int idReporte, string company)
        {
            try
            {
                string connStr = _configuration.GetConnectionString("ArcaTrazability");
                var dato = AuditHistorialService.ObtenerLoteHoraQuejaPorId(_configuration, connStr, "HistorialReportesById", idReporte);

                if (dato == null)
                {
                    _logger.LogWarning("Report not found with ID: {ReportId}", idReporte);
                    return NotFound();
                }

                var hora = (dato.HoraQueja ?? new TimeSpan(9, 0, 0)).ToString(@"hh\:mm\:ss");

                _logger.LogInformation("Redirecting to AuditReport from history. ReportId: {ReportId}, Lote: {Lote}", idReporte, dato.Lote);
                return RedirectToAction("AuditReport", new { lote = dato.Lote, horaQueja = hora, company });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auditing from history. ReportId: {ReportId}", idReporte);
                return StatusCode(500, "Error loading report from history");
            }
        }



        private BatchInfo ObtenerBatchDesdeLote(string lote, string connStr, string company)
        {
            return TrazabilityClass.GetBatchInfoByText(lote, connStr, company);
        }

        private List<TrazabilidadNode> ObtenerTrazabilidadCompleta(string company, long batch, string connStr)
        {
            var dt = TrazabilityClass.GetTraceabilityAudit(company, batch, connStr);
            return TrazabilityClass.ConvertirDataTableATrazabilidad(dt);
        }

        private BloqueMateriaPrimaModel ObtenerBloqueMateriaPrima(List<TrazabilidadNode> nodos, string company, string connStr)
        {
            var materiaPrima = nodos
                .Where(x => x.IsRawMaterial)
                .GroupBy(x => x.Batch)
                .Select(g => g.First())
                .ToList();

            var descripciones = materiaPrima
                .GroupBy(x => x.Batch)
                .ToDictionary(
                    g => g.Key,
                    g => new LoteDescripcionInfo
                    {
                        BatchName = g.First().BatchName ?? "Sin ID",
                        ManufacturingReferenceName = g.First().ManufacturingReferenceName ?? "Sin nombre"
                    });

            var parametros = new Dictionary<string, object>
    {
        { "@company", company },
        { "{lotes}", string.Join(",", materiaPrima.Select(x => x.Batch)) }
    };

            var descripcionesString = descripciones.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ManufacturingReferenceName);

            var registrosMP = DynamicSqlService.EjecutarQuery<RegistroMateriaPrima>(
                "RegistroMateriaPrima", parametros, descripcionesString, _configuration, connStr
            );

            foreach (var registro in registrosMP)
            {
                var loteId = descripciones
                    .Where(kvp => kvp.Value.ManufacturingReferenceName == registro.Descripcion)
                    .Select(kvp => kvp.Key)
                    .FirstOrDefault();

                if (loteId > 0 && descripciones.ContainsKey(loteId))
                {
                    registro.LoteInterno = descripciones[loteId].BatchName;
                }
            }

            return new BloqueMateriaPrimaModel
            {
                Registros = registrosMP,
                SupervisorAlmacen = "Raul Juarez",
                SupervisorCalidad = "Margarita Antonio"
            };
        }


        private BloqueProductoTerminadoModel ObtenerBloqueProductoTerminado(long batch, string company, string connStr)
        {
            var parametros = new Dictionary<string, object>
    {
        { "@company", company },
        { "@lote", batch }
    };

            var lista = DynamicSqlService.EjecutarQuery<BloqueProductoTerminadoModel>(
                "RegistroProductoTerminado", parametros, null, _configuration, connStr
            );

            var modelo = lista.FirstOrDefault() ?? new BloqueProductoTerminadoModel();

            modelo.EncargadoPruebas = "Karen Choul Garza";
            modelo.SupervisorJarabes = "Yahaira Luna";
            modelo.VacioJarabeTerminado = "Milton / Cristian";
            modelo.SupervisorCalidad = "Marco Robles";
            modelo.SupervisorProduccion = "Ismael Lara";
            modelo.SupervisorMantenimiento = "Ismael Lara";
            modelo.Llenadora = "3";

            return modelo;
        }





    }
}
