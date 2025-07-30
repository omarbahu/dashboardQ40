using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using static dashboardQ40.Models.Models;
using static dashboardQ40.Services.AuditTrazabilityClass;

namespace dashboardQ40.Controllers
{
    public class TrazabilidadAuditoriaController : Controller
    {
        private readonly IConfiguration _configuration;

        public TrazabilidadAuditoriaController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GuardarReporte([FromBody] ReporteTrazabilidad modelo)
        {
            // Validación simple (puedes expandirla)
            if (!ModelState.IsValid)
            {
                var errores = ModelState
                    .Where(kvp => kvp.Value.Errors.Any())
                    .Select(kvp => $"{kvp.Key}: {string.Join(", ", kvp.Value.Errors.Select(e => e.ErrorMessage))}")
                    .ToList();

                return BadRequest("Datos inválidos: " + string.Join(" | ", errores));
            }

            // Aquí deberías guardar a base de datos
            // Ejemplo simple (esto es solo un placeholder)
            // _context.Reportes.Add(modelo);
            // _context.SaveChanges();

            return Ok(new { success = true, mensaje = "Reporte guardado correctamente" });
        }



        public async Task<IActionResult> AuditReport(string lote, TimeSpan horaQueja)
        {
            if (string.IsNullOrWhiteSpace(lote)) return View();

            string connStr = _configuration.GetConnectionString("CaptorConnection");
            string company = _configuration.GetConnectionString("company");

            var batchInfo = ObtenerBatchDesdeLote(lote, connStr, company);
            if (batchInfo.BatchId <= 0) return View();

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

            // 2. Buscar el que estaba activo a la hora de la queja
            var jarabeActivo = nodosJarabeTerminado
                .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

            var batchInfoJT = new BatchInfo(); 

            if (jarabeActivo != null)
            {
                batchInfoJT = ObtenerBatchDesdeLote(jarabeActivo.BatchIdentifier, connStr, company);
            }

            var bloqueLiberacion = await AuditTrazabilityClass.ObtenerPruebasLiberacionJarabeTerminadoAsync(
                batchInfo.StartDate,
                batchInfo.EndDate,
                batchInfo.workplace,
                company,
                batchInfo.manufacturingReference,
                connStr);

            var modelo = new FormatoViewModel
            {
                Lote = lote,
                BloqueMateriaPrima = bloqueMateriaPrima,
                BloqueProductoTerminado = bloqueProdTerm,
                BloqueRegistroTiempo = AuditTrazabilityClass.ObtenerEntregaInformacion(lote),
                //BloqueJarabesLoteJarabeSimple = AuditTrazabilityClass.ObtenerJarabeSimplePorLote(lote),                
                
                // Jarabe simple
                BloqueJarabesLoteJarabeSimpleContacto = AuditTrazabilityClass.ObtenerJarabeSimpleConContacto(trazabilidadNodos, batchInfo.BatchId, horaQueja),
                BloqueAnalisisFisicoquimicoJarabeSimple = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoJarabeSimple(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration),
                BloqueAnalisisSensorialJarabeSimple = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeSimple(lote),

                // Jarabe terminado
                BloqueJarabeTerminado = AuditTrazabilityClass.ObtenerJarabeTerminado(trazabilidadNodos, batchInfo.BatchId, horaQueja, batchInfoJT),
                BloqueAnalisisFisicoquimicoJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoJarabeTerminado(trazabilidadNodos, batchInfo.BatchId, horaQueja, company, connStr, _configuration),
                BloqueAnalisisSensorialJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeTerminado(lote),

                

                BloquePruebasLiberacion = bloqueLiberacion
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
