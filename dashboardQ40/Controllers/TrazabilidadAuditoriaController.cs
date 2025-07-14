using dashboardQ40.Models;
using Microsoft.AspNetCore.Mvc;

namespace dashboardQ40.Controllers
{
    public class TrazabilidadAuditoriaController : Controller
    {
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

        public IActionResult AuditReport()
        {
            return View();
        }
    }
}
