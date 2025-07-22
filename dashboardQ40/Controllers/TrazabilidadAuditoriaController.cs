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

        public IActionResult AuditReport(string lote)
        {
            if (!string.IsNullOrEmpty(lote))
            {
                string connStr = _configuration.GetConnectionString("CaptorConnection");
                string company = _configuration.GetConnectionString("company");

                BatchInfo batchInfo = TrazabilityClass.GetBatchInfoByText(lote, connStr, company);
                long batch = batchInfo.BatchId;
                if (batch > 0)
                {
                    DataTable trazabilidad = TrazabilityClass.GetTraceabilityAudit(company, batch, connStr);
                    List<TrazabilidadNode> trazabilidadNodos = TrazabilityClass.ConvertirDataTableATrazabilidad(trazabilidad);
                    List<TrazabilidadNode> materiaPrimaLotes = trazabilidadNodos
                        .Where(x => x.IsRawMaterial)
                        .GroupBy(x => x.Batch) // evitar duplicados
                        .Select(g => g.First())
                        .ToList();

                    List<TrazabilidadNode> nodosJarabeSimple = trazabilidadNodos
                        .Where(x => x.ManufacturingReferenceName.ToUpper().Contains("JARABE SIMPLE"))
                        .GroupBy(x => x.Batch)
                        .Select(g => g.First())
                        .ToList();

                    // Creamos el diccionario de descripciones (batch -> manufacturingReferenceName)
                    var descripciones = materiaPrimaLotes
                    .GroupBy(x => x.Batch) // Evita duplicados por lote
                    .ToDictionary(
                    g => g.Key, // Batch (long)
                    g => new LoteDescripcionInfo
                    {
                        BatchName = g.First().BatchName ?? "Sin ID",
                        ManufacturingReferenceName = g.First().ManufacturingReferenceName ?? "Sin nombre"
                    });

                    // Obtenemos los datos desde base de datos con los lotes y descripciones
                    var parametros = new Dictionary<string, object>
                    {
                        { "@company", company },
                        { "{lotes}", string.Join(",", materiaPrimaLotes.Select(x => x.Batch)) }
                    };

                    var descripcionesString = descripciones.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ManufacturingReferenceName);

                    var registrosMP = DynamicSqlService.EjecutarQuery<RegistroMateriaPrima>(
                        "RegistroMateriaPrima",
                        parametros,
                        descripcionesString,
                        _configuration,
                        connStr
                    );

                    foreach (var registro in registrosMP)
                    {
                        // Encontrar el loteId correspondiente a la descripción
                        var loteId = descripciones
                            .Where(kvp => kvp.Value.ManufacturingReferenceName == registro.Descripcion)
                            .Select(kvp => kvp.Key)
                            .FirstOrDefault();

                        if (loteId > 0 && descripciones.ContainsKey(loteId))
                        {
                            registro.LoteInterno = descripciones[loteId].BatchName;
                        }
                    }

                    var parametros2 = new Dictionary<string, object>
                    {
                        { "@company", company },
                        { "@lote", batch }
                    };

                    var registroProdTermList = DynamicSqlService.EjecutarQuery<BloqueProductoTerminadoModel>(
                        "RegistroProductoTerminado",  // ojo, que este es el nombre correcto del bloque en appsettings
                        parametros2,
                        null,
                        _configuration,
                        connStr
                    );

                    var registroProdTerm = registroProdTermList.FirstOrDefault() ?? new BloqueProductoTerminadoModel();

                    registroProdTerm.EncargadoPruebas = "Karen Choul Garza";
                    registroProdTerm.SupervisorJarabes = "Yahaira Luna";
                    registroProdTerm.VacioJarabeTerminado = "Milton / Cristian";
                    registroProdTerm.SupervisorCalidad = "Marco Robles";
                    registroProdTerm.SupervisorProduccion = "Ismael Lara";
                    registroProdTerm.SupervisorMantenimiento = "Ismael Lara";
                    registroProdTerm.Llenadora = "3";

                    var parametrosJarabe = new Dictionary<string, object>
                    {
                        { "@company", company },
                        { "@lotes", nodosJarabeSimple.Select(x => x.Batch).ToList() }
                    };

                    var modelo = new FormatoViewModel
                    {
                        Lote = lote,

                        BloqueMateriaPrima = new BloqueMateriaPrimaModel
                        {
                            Registros = registrosMP,
                            SupervisorAlmacen = "Raul Juarez",
                            SupervisorCalidad = "Margarita Antonio"
                        },
                        //BloqueProductoTerminadoModel
                        //BloqueProductoTerminado = AuditTrazabilityClass.ObtenerDatosProductoTerminado(lote),
                        BloqueProductoTerminado = registroProdTerm,
                        BloqueRegistroTiempo = AuditTrazabilityClass.ObtenerEntregaInformacion(lote),
                        BloqueJarabesLoteJarabeSimple = AuditTrazabilityClass.ObtenerJarabeSimplePorLote(lote),
                        BloqueJarabesLoteJarabeSimpleContacto = AuditTrazabilityClass.ObtenerJarabeSimpleConContacto(lote),
                        BloqueAnalisisSensorialJarabeSimple = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeSimple(lote),
                        BloqueJarabeTerminado = AuditTrazabilityClass.ObtenerJarabeTerminado(lote),
                        BloqueAnalisisFisicoquimicoJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisFisicoquimicoJarabeTerminado(lote),
                        BloqueAnalisisSensorialJarabeTerminado = AuditTrazabilityClass.ObtenerAnalisisSensorialJarabeTerminado(lote),
                        BloquePruebasLiberacion = AuditTrazabilityClass.ObtenerPruebasLiberacion(lote)
                        //BloqueProductoTerminado = AuditTrazabilityClass.ObtenerDatosProductoTerminado(),
                        //BloqueJarabes = AuditTrazabilityClass.ObtenerDatosJarabes(),
                        // ...
                    };

                    return View(modelo);
                }
                else
                {
                    return View();
                }
            }
            else
            {
                return View();
            }
        }
    }
}
