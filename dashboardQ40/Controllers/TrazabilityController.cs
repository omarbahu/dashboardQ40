using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Data;
using static dashboardQ40.Models.Models;

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




    }


}
