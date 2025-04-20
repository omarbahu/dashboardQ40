using dashboardQ40.DAL;
using dashboardQ40.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardWidgetsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardWidgetsController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 Obtiene un widget por ID
        [HttpGet("{id}")]
        public IActionResult GetWidgetById(int id)
        {
            var widget = _context.DashboardWidgets.FirstOrDefault(w => w.WidgetID == id);
            if (widget == null)
                return NotFound("Widget no encontrado.");

            return Ok(widget);
        }

        // 🔹 Actualiza un widget
        [HttpPut("{id}")]
        public IActionResult UpdateWidget(int id, [FromBody] DashboardWidget model)
        {
            var existingWidget = _context.DashboardWidgets.FirstOrDefault(w => w.WidgetID == id);
            if (existingWidget == null)
                return NotFound("Widget no encontrado.");

            existingWidget.WidgetType = model.WidgetType;
            existingWidget.Position = model.Position;
            existingWidget.Config = model.Config;
            existingWidget.DataSource = model.DataSource;

            _context.SaveChanges();
            return NoContent();
        }

        // 🔹 Elimina un widget
        [HttpDelete("{id}")]
        public IActionResult DeleteWidget(int id)
        {
            var widget = _context.DashboardWidgets.FirstOrDefault(w => w.WidgetID == id);
            if (widget == null)
                return NotFound("Widget no encontrado.");

            _context.DashboardWidgets.Remove(widget);
            _context.SaveChanges();

            return NoContent();
        }

       
    }
}
