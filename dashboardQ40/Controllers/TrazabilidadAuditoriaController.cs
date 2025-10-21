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

        [HttpPost]
        public IActionResult GuardarReporte([FromBody] ReporteTrazabilidad modelo)
        {
            if (!ModelState.IsValid)
            {
                var errores = ModelState
                    .Where(kvp => kvp.Value.Errors.Any())
                    .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();

                return BadRequest(new { success = false, mensaje = "Datos inválidos", detalles = errores });
            }

            try
            {
                if (modelo.simulate)
                {
                    // 🔹 MODO SIMULACIÓN: no persistir
                    // (puedes hacer validaciones/cálculos previos aquí)
                    return Ok(new
                    {
                        success = true,
                        simulated = true,
                        mensaje = "Simulación realizada. No se guardó en la base de datos."
                    });
                }

                // 🔹 MODO GUARDADO REAL
                // _context.Reportes.Add(modelo);
                // _context.SaveChanges();

                return Ok(new
                {
                    success = true,
                    simulated = false,
                    mensaje = "Reporte guardado correctamente."
                });
            }
            catch (Exception ex)
            {
                // log ex ...
                return StatusCode(500, new { success = false, mensaje = "Error al guardar el reporte." });
            }
        }




        public async Task<IActionResult> AuditReport(string lote, TimeSpan horaQueja, string company)
        {

            var token = await _authService.ObtenerTokenCaptor(_settings.Company);

            if (string.IsNullOrWhiteSpace(lote)) return View();

            string connStr = _configuration.GetConnectionString("CaptorConnection");
            //string company = _configuration.GetConnectionString("company");

            var batchInfo = ObtenerBatchDesdeLote(lote, connStr, company);
            if (batchInfo == null || batchInfo.BatchId <= 0)
            {
                // Opción 1: Muestra una vista vacía o con mensaje personalizado
                ViewBag.Mensaje = $"El lote '{lote}' no fue encontrado o no tiene información.";
                return View("LoteNoEncontrado");

                // Opción 2: Redirige a otra página
                // return RedirectToAction("Index");
            }            

            var trazabilidadNodos = ObtenerTrazabilidadCompleta(company, batchInfo.BatchId, connStr);
            
            var bloqueMateriaPrima = ObtenerBloqueMateriaPrima(trazabilidadNodos, company, connStr);
            //var bloqueMateriaPrimaWS = getBloqueMateriaPrima(token.access_token, _settings.BaseUrl + _settings.QueryMP, company, _settings.trazalog, "getMP", lotes);
            var bloqueProdTerm = ObtenerBloqueProductoTerminado(batchInfo.BatchId, company, connStr);


            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchInfo.BatchId);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            var nodosJarabeTerminado = trazabilidadNodos
               .Where(n => n.ManufacturingReferenceName.ToUpper().Contains("JARABE TERMINADO"))
               .OrderBy(n => n.StartDate)
               .ToList();

            // 2. Buscar el que estaba activo a la hora de la queja
            var jarabeterminadoActivo = nodosJarabeTerminado
                .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

            var nodosJarabeSimple = trazabilidadNodos
   .Where(n => n.ManufacturingReferenceName.ToUpper().Contains("JARABE SIMPLE"))
   .OrderBy(n => n.StartDate)
   .ToList();

            // 2. Buscar el que estaba activo a la hora de la queja
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

            var bloqueLiberacion = await AuditTrazabilityClass.ObtenerPruebasLiberacionJarabeTerminadoAsync(batchInfo.StartDate,batchInfo.EndDate,batchInfo.workplace,company,batchInfo.manufacturingReference,connStr);
            var registrotiempo = AuditTrazabilityClass.ObtenerEntregaInformacion(lote);
            //var LoteJarabeSimple = AuditTrazabilityClass.ObtenerJarabeSimpleConContacto(trazabilidadNodos, batchInfo.BatchId, horaQueja);
            
            // bloque de jarabes 
            (BloqueLotesPrincipalesModel LoteJarabeSimple, TrazabilidadNode? jarabeSimpleActivo2) = AuditTrazabilityClass.ObtenerJarabeSimpleConContacto(
    trazabilidadNodos, batchInfo.BatchId, horaQueja);
            var AnalisisFisicoquimicoJarabeSimple = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F002", "ANÁLISIS FISICOQUÍMICOS DE JARABE SIMPLE", "AnalisisFisicoquimico");
            var AnalisisSensorialJarabeSimple = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeSimple(lote);
            var JarabeTerminado = AuditTrazabilityClass.ObtenerJarabeTerminado(trazabilidadNodos, batchInfo.BatchId, horaQueja, batchInfoJT);
            var AnalisisFisicoquimicoJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F003", "ANÁLISIS FISICOQUÍMICOS DE JARABE TERMINADO", "AnalisisFisicoquimico");
            var AnalisisSensorialJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeTerminado(lote);
            
            // BLOQUE MATERIAS PRIEMAS - FALTAN LOS ANALISIS SENSORIALES
            var Azucar = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F001", "AZÚCAR", "AnalisisFisicoquimico");
            var fructuosaSILO1 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F007", "FRUCTOSA SILO 1", "AnalisisFisicoquimico");
            // para saber en la fructuosa si esta en el silo 1 o el que sea, hay que buscar el almacen que contanga el loete que buscamos "frucuosa" y en la entrada de proveedor en la parte de ubicacion vendra el numero de silo

            var co2 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F013", "CO2", "AnalisisFisicoquimico");
            var NITROGENO = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F016", "NITROGENO", "AnalisisFisicoquimico");
            var fructuosaSILO2 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F007", "FRUCTOSA SILO 2", "AnalisisFisicoquimico");
            var fructuosaSILO3 = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F007", "FRUCTOSA SILO 2", "AnalisisFisicoquimico");


            //BLOQUE TRATAMIENDO DE AGUA
            var aguatratada = AuditTrazabilityClass.ObtenerTratamientodeAgua(trazabilidadNodos, batchInfo.BatchId, horaQueja, batchInfoJT);
            var aguatratadajarabesimple = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfoJS.BatchId, horaQueja, company, connStr, _configuration, "F014", "AGUA TRATADA UTILIZADA EN JARABE SIMPLE", "AnalisisFisicoquimico");
            var aguatratadajarabeterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfoJT.BatchId, horaQueja, company, connStr, _configuration, "F014", "AGUA TRATADA UTILIZADA EN JARABE TERMINADO", "AnalisisFisicoquimico");
            var aguatratadaproductoterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F014", "AGUA TRATADA UTILIZADA EN PRODUCTO TERMINADO", "AnalisisFisicoquimico");
            var aguatratadacruda = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F017", "AGUA CRUDA, TRATADA CLORADA,SUAVIZADA O RECUPERADA, PARA EL ENJUAGUE DEL ENVASE (EN CASO DE QUE APLIQUE)", "AnalisisFisicoquimico");
            var aguatratadasuave = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F018", "AGUA SUAVE UTILIZADA", "AnalisisFisicoquimico");

            // bloque datos de limpieza y saneamiento
            var saneo = AuditTrazabilityClass.ObtenerSaneo(trazabilidadNodos, batchInfo.BatchId, horaQueja, batchInfoJT);
            var saneojarabesimple = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfoJS.BatchId, horaQueja, company, connStr, _configuration, "F015", "SANEO UTILIZADA EN JARABE SIMPLE", "AnalisisFisicoquimico");
            var saneojarabeterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfoJT.BatchId, horaQueja, company, connStr, _configuration, "F015", "SANEO UTILIZADA EN JARABE TERMINADO", "AnalisisFisicoquimico");
            var asaneoproductoterminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimico(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration, "F015", "SANEO UTILIZADA EN PRODUCTO TERMINADO", "AnalisisFisicoquimico");


            var modelo = new FormatoViewModel
            {
                Lote = lote,
                
                //datos para formato 
                BloqueMateriaPrima = bloqueMateriaPrima,
                BloqueProductoTerminado = bloqueProdTerm,
                BloqueRegistroTiempo = registrotiempo,                
                // Jarabe simple
                BloqueJarabesLoteJarabeSimpleContacto = LoteJarabeSimple,                
                BloqueAnalisisFisicoquimicoJarabeSimple = AnalisisFisicoquimicoJarabeSimple,                
                BloqueAnalisisSensorialJarabeSimple = AnalisisSensorialJarabeSimple,
                // Jarabe terminado
                BloqueJarabeTerminado = JarabeTerminado,
                //"ANÁLISIS FISICOQUÍMICOS DE JARABE SIMPLE"  "JARABE SIMPLE" "AnalisisFisicoquimicoJarabeSimple"
                BloqueAnalisisFisicoquimicoJarabeTerminado = AnalisisFisicoquimicoJarabeTerminado,
                BloqueAnalisisSensorialJarabeTerminado = AnalisisSensorialJarabeTerminado,
                BloquePruebasLiberacion = bloqueLiberacion,
                //datos para materias primas 
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
                Bloqueaguatratadasuave  = aguatratadasuave,

                Bloquesaneo = saneo,
                Bloquesaneojarabesimple = saneojarabesimple,
                Bloquesaneojarabeterminado = saneojarabeterminado,
                Bloqueasaneoproductoterminado = asaneoproductoterminado
            };

            

            return View(modelo);
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

            // Asignar valores manuales
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
