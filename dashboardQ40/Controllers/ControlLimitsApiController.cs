// Controllers/ControlLimitsApiController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using dashboardQ40.Services;
using dashboardQ40.Models;

namespace dashboardQ40.Controllers
{
    [ApiController]
    [Route("api/controllimits")]
    public sealed class ControlLimitsApiController : ControllerBase
    {
        private readonly ControlLimitsService _svc;
        private readonly AuthService _authService;
        private readonly WebServiceSettings _settings;

        public ControlLimitsApiController(
            ControlLimitsService svc,
            AuthService authService,
            IOptions<WebServiceSettings> settings)
        {
            _svc = svc; _authService = authService; _settings = settings.Value;
        }

        // GET /api/controllimits/candidates?company=001&f1=2025-01-01&f2=2025-09-25&minN=100&minCpk=1.33&cpkTarget=1.40
        // GET /api/controllimits/candidates?company=001&f1=2025-01-01&f2=2025-09-25&minN=100&maxCpk=1.33
        [HttpGet("candidates")]
        public async Task<IActionResult> Candidates(
            [FromQuery] string company,
            [FromQuery] DateTime f1,
            [FromQuery] DateTime f2,
            [FromQuery] int minN = 100,
            [FromQuery] double maxCpk = 1.33,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(company))
                return BadRequest(new { error = "Parametro 'company' requerido." });

            if (f1 >= f2)
                return BadRequest(new { error = "F1 debe ser menor que F2." });

            string? token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
            {
                var tk = await _authService.ObtenerTokenCaptor(company != _settings.Company ? company : _settings.Company);
                if (tk == null || string.IsNullOrWhiteSpace(tk.access_token))
                    return Unauthorized(new { error = "No se pudo obtener el token." });

                token = tk.access_token;
                HttpContext.Session.SetString("AuthToken", token);
            }

            try
            {
                var data = await _svc.GetCandidatesRangeAsync(
                    token, company, f1, f2, minN, maxCpk, ct);

                return Ok(data);
            }
            catch (Exception ex)
            {
                return Problem(title: "Error al obtener candidatos", detail: ex.Message, statusCode: 500);
            }
        }


        // GET /api/controllimits/timeseries?company=001&sku=0799&variable=TR-Y-7-201&f1=2025-01-01&f2=2025-09-30
        [HttpGet("timeseries")]
        public async Task<IActionResult> TimeSeries(
     [FromQuery] string company,
     [FromQuery] string sku,
     [FromQuery] string variable,
     [FromQuery] DateTime f1,
     [FromQuery] DateTime f2,
     CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(variable))
                return BadRequest(new { error = "company, sku y variable son requeridos." });
            if (f1 > f2) return BadRequest(new { error = "f1 debe ser menor que f2." });

            var periodStart = DateTime.SpecifyKind(f1.Date, DateTimeKind.Unspecified);
            var periodEnd = DateTime.SpecifyKind(f2.Date.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);

            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token)) return Unauthorized(new { error = "No token en sesión" });

            var dto = await _svc.GetSeriesForChartAsync(token, company, sku, variable, periodStart, periodEnd, ct);

            return Ok(new
            {
                values = dto.Values,          // [v1, v2, ...]
                mean = dto.Mean,            // μ
                sigma = dto.Sigma,           // σ
                lsl = dto.Lsl,
                usl = dto.Usl
            });
        }


    }
}
