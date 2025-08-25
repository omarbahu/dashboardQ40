using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;
using static dashboardQ40.Models.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

using System.Data.SqlClient;
using System.Linq;                         // Linq



namespace dashboardQ40.Controllers
{
    public class TrazabilityController : Controller
    {
        private readonly IConfiguration _configuration;

        public TrazabilityController(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult ReportTrazability(string searchBatch, DateTime? startDate, DateTime? endDate)
        {
            var model = new TrazabilidadConChecklistViewModel
            {
                SearchBatch = searchBatch,
                StartDate = startDate,
                EndDate = endDate
            };

            if (!string.IsNullOrEmpty(searchBatch))
            {
                string connStr = _configuration.GetConnectionString("CaptorConnection");
                string company = _configuration.GetConnectionString("company");

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
        public async Task<IActionResult> BuscarLotesPorFechas(DateTime fechaIni, DateTime fechaFin)
        {
            // Normaliza fin al final del día si quieres incluir todo el día
            var fin = fechaFin.Date.AddDays(1).AddTicks(-1);

            string connStr = _configuration.GetConnectionString("CaptorConnection");
            string company = _configuration.GetConnectionString("company");

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
