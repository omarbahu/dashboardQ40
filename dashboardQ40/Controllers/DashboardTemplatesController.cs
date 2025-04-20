using dashboardQ40.DAL;
using dashboardQ40.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardTemplatesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardTemplatesController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 Devuelve todas las plantillas (optimizado)
        [HttpGet]
        public IActionResult GetAllTemplates()
        {
            var templates = _context.DashboardTemplates
                .Select(t => new { t.TemplateID, t.TemplateName, t.Planta, t.Linea, t.VariableY })
                .ToList();

            return Ok(templates);
        }

        // 🔹 Obtiene una plantilla por ID
        [HttpGet("{id}")]
        public IActionResult GetTemplateById(int id)
        {
            var template = _context.DashboardTemplates
                .Include(t => t.Widgets)
                .FirstOrDefault(t => t.TemplateID == id);

            if (template == null)
                return NotFound("Plantilla no encontrada.");

            return Ok(template);
        }

        // 🔹 Crea una nueva plantilla (admin)
        [HttpPost("admin/create")]
        public IActionResult CreateOrUpdateTemplate([FromBody] DashboardTemplateCreateModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.Planta) || string.IsNullOrEmpty(model.Linea) || string.IsNullOrEmpty(model.VariableY))
                return BadRequest("Faltan datos requeridos.");

            var existingTemplate = _context.DashboardTemplates
                .FirstOrDefault(t => t.Planta == model.Planta && t.Linea == model.Linea && t.VariableY == model.VariableY);

            if (existingTemplate != null)
            {
                // 🚀 Si el template existe, actualiza solo el nombre
                existingTemplate.TemplateName = model.TemplateName;
                //existingTemplate.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // 🆕 Si no existe, crea uno nuevo
                existingTemplate = new DashboardTemplate
                {
                    TemplateName = model.TemplateName,
                    Planta = model.Planta,
                    Linea = model.Linea,
                    VariableY = model.VariableY,
                    CreatedBy = "admin",
                    CreatedAt = DateTime.UtcNow
                };

                _context.DashboardTemplates.Add(existingTemplate);
            }

            _context.SaveChanges();

            // Ahora agregamos los widgets
            if (model.Widgets != null && model.Widgets.Count > 0)
            {
                foreach (var widget in model.Widgets)
                {
                    var existingWidget = _context.DashboardWidgets
                        .FirstOrDefault(w => w.TemplateID == existingTemplate.TemplateID && w.VariableX == widget.VariableX && w.WidgetType == widget.WidgetType);

                    if (existingWidget == null)
                    {
                        _context.DashboardWidgets.Add(new DashboardWidget
                        {
                            TemplateID = existingTemplate.TemplateID,
                            VariableX = widget.VariableX,
                            WidgetType = widget.WidgetType,
                            Position = widget.Position,
                            Config = widget.Config,
                            DataSource = widget.DataSource,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                _context.SaveChanges();
            }

            return CreatedAtAction(nameof(GetTemplateById), new { id = existingTemplate.TemplateID }, existingTemplate);
        }



/*
        // 🔹 Obtiene un dashboard según Planta, Línea y Variable Y (usuarios normales)
        [HttpGet("user/load")]
        public IActionResult GetDashboardByFilters(string planta, string linea, string variableY)
        {
            var template = _context.DashboardTemplates
                .Include(t => t.Widgets)
                .FirstOrDefault(t => t.Planta == planta && t.Linea == linea && t.VariableY == variableY);

            if (template == null)
                return NotFound("No se encontró una plantilla para los filtros seleccionados.");

            return Ok(template);
        }
*/

        // 🔹 Obtiene una plantilla con sus widgets (admin)
        [HttpGet("{id}/full")]
        public IActionResult GetTemplateWithRelations(int id)
        {
            var template = _context.DashboardTemplates
                .Include(t => t.Widgets)
                .FirstOrDefault(t => t.TemplateID == id);

            if (template == null)
                return NotFound("Plantilla no encontrada.");

            return Ok(template);
        }

        [HttpPost("{templateId}/widgets")]
        public IActionResult AddWidgetToTemplate(int templateId, [FromBody] DashboardWidgetCreateModel model)
        {
            var template = _context.DashboardTemplates.FirstOrDefault(t => t.TemplateID == templateId);
            if (template == null)
                return NotFound("Plantilla no encontrada.");

            // 🚀 Validar si el widget ya existe en la plantilla para evitar duplicados
            var existingWidget = _context.DashboardWidgets
                .FirstOrDefault(w => w.TemplateID == templateId && w.VariableX == model.VariableX && w.WidgetType == model.WidgetType);

            if (existingWidget != null)
                return BadRequest("Este widget ya está agregado a la plantilla.");

            var widget = new DashboardWidget
            {
                TemplateID = templateId,
                VariableX = model.VariableX,  // 🔹 Ahora usa VariableX en lugar de Variable
                WidgetType = model.WidgetType,
                Position = model.Position,
                Config = model.Config,
                DataSource = model.DataSource,
                CreatedAt = DateTime.UtcNow
            };

            _context.DashboardWidgets.Add(widget);
            _context.SaveChanges();

            return CreatedAtAction(nameof(GetTemplateWithRelations), new { id = templateId }, widget);
        }

        [HttpGet("user/load")]
        public IActionResult GetUserDashboard(string planta, string linea, string variableY)
        {
            if (string.IsNullOrWhiteSpace(planta) || string.IsNullOrWhiteSpace(linea) || string.IsNullOrWhiteSpace(variableY))
                return BadRequest("Faltan datos requeridos.");

            var template = _context.DashboardTemplates
                .Include(t => t.Widgets)
                .FirstOrDefault(t =>
                    t.Planta.Trim().ToLower() == planta.Trim().ToLower() &&
                    t.Linea.Trim().ToLower() == linea.Trim().ToLower() &&
                    t.VariableY.Trim().ToLower() == variableY.Trim().ToLower());

            if (template == null)
                return NotFound("No se encontró un dashboard para los filtros seleccionados.");

            // ✅ Convertimos la lista de Widgets a la clase `DashboardWidgetDTO`
            List<DashboardWidgetDTO> widgetsList = template.Widgets?
                .Select(w => new DashboardWidgetDTO
                {
                    VariableX = w.VariableX,
                    WidgetType = w.WidgetType,
                    Position = w.Position
                }).ToList() ?? new List<DashboardWidgetDTO>(); // 🔹 Ahora siempre tiene un tipo definido

            return new JsonResult(new
            {
                template.TemplateName,
                widgets = widgetsList
            }, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }



    }
}
