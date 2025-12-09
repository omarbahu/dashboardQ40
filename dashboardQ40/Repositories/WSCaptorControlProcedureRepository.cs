using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using dashboardQ40.Helpers;
using dashboardQ40.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace dashboardQ40.Repositories
{
    public class WSCaptorControlProcedureRepository
    {
        private readonly WebServiceSettings _settings;
        private readonly ILogger<WSCaptorControlProcedureRepository> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public WSCaptorControlProcedureRepository(
            IOptions<WebServiceSettings> settings,
            ILogger<WSCaptorControlProcedureRepository> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Lee todas las filas de ControlProcedure para un CP (todas las versiones/niveles).
        /// Equivale a GET /Rest/TableRow/ControlProcedure/{company}/{controlProcedure}
        /// </summary>
        public async Task<List<ControlProcedureRow>> GetControlProcedureAsync(
            string token,
            string company,
            string controlProcedure,
            CancellationToken ct = default)
        {
            // BaseUrl ya lo usas en otros servicios para Captor, reutilizamos.
            var url = $"{_settings.BaseUrl}/Rest/TableRow/ControlProcedure/{company}/{controlProcedure}";

            using var client = common.Method_Headers(token, url); // mismo helper que ya usas

            _logger.LogInformation("GET {Url}", url);

            using var resp = await client.GetAsync(client.BaseAddress!, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);

            var rows = JsonSerializer.Deserialize<List<ControlProcedureRow>>(json, _jsonOptions)
                       ?? new List<ControlProcedureRow>();

            return rows;
        }


        public async Task<List<ControlProcedureOperationRow>> GetOperationsForCompanyAsync(
    string token,
    string company,
    CancellationToken ct = default)
        {
            // IMPORTANTE: aquí pedimos TODAS las operaciones de la compañía
            // GET /Rest/TableRow/ControlProcedureOperation/{company}
            var url = $"{_settings.BaseUrl}/Rest/TableRow/ControlProcedureOperation/{company}";

            using var client = common.Method_Headers(token, url);

            _logger.LogInformation("GET {Url}", url);

            using var resp = await client.GetAsync(client.BaseAddress!, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);

            var rows = JsonSerializer.Deserialize<List<ControlProcedureOperationRow>>(json, _jsonOptions)
                       ?? new List<ControlProcedureOperationRow>();

            return rows;
        }



    }
}
