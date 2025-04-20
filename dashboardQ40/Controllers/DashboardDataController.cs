using dashboardQ40.DAL;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dashboardQ40.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardDataController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardDataController(AppDbContext context)
        {
            _context = context;
        }

        // ✅ Endpoint para obtener datos de gráficos dinámicos
        [HttpGet("data")]
        public IActionResult GetChartData(string line, string product, string variableY, string variablesX)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(product) || string.IsNullOrEmpty(variableY) || string.IsNullOrEmpty(variablesX))
            {
                return BadRequest("Faltan parámetros requeridos.");
            }

            // Simular datos para prueba
            var random = new Random();
            var data = new Dictionary<string, List<int>>();

            var variables = variablesX.Split(',');

            foreach (var variable in variables)
            {
                data[variable] = Enumerable.Range(0, 10).Select(_ => random.Next(1, 100)).ToList();
            }

            return Ok(new { variableY, data });
        }

        [HttpGet]
        public IActionResult GetData([FromQuery] string planta, [FromQuery] string linea, [FromQuery] string variableY, [FromQuery] string variableX)
        {
            if (string.IsNullOrEmpty(planta) || string.IsNullOrEmpty(linea) || string.IsNullOrEmpty(variableY) || string.IsNullOrEmpty(variableX))
            {
                return BadRequest("Faltan parámetros requeridos.");
            }

            // 🔹 Simulación de datos para la Variable X seleccionada
            var random = new Random();
            var data = new List<int>();

            for (int i = 0; i < 10; i++)
            {
                data.Add(random.Next(50, 100)); // Valores aleatorios simulados
            }

            return Ok(new
            {
                planta,
                linea,
                variableY,
                variableX,
                data
            });
        }
    }
}
