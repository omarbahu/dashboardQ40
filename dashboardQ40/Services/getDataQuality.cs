using dashboardQ40.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using Serilog;
using static dashboardQ40.Services.common;
using static dashboardQ40.Models.Models;
using System.Net.Http.Headers;

namespace dashboardQ40.Services
{
    public class getDataQuality
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthService> _logger;
        private readonly WebServiceSettings _settings;
        private result_token _cachedToken;
        private DateTime _tokenExpiration;

        public getDataQuality(HttpClient httpClient, ILogger<AuthService> logger, IOptions<WebServiceSettings> settings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value; // Extrae el valor de IOptions
        }

        public static async Task<result_Q_Lineas> getLinesByCompany(string token, string url, string company, string trazalog)
        {
            HttpClient client = Method_Headers(token, url);

            var jsonBody = "{ 'COMP': '" + company + "' }";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_Lineas>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "getLinesByCompany",
                trazalog
            );
        }


        public static async Task<result_Q_Productos> getProductsByLine(string token, string url, string company, string lineaId, DateTime fechaInicial, DateTime fechaFinal)
        {
            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss");

            HttpClient client = Method_Headers(token, url);

            var jsonBody = "{ 'COMP': '" + company + "','WP': '" + lineaId + "', 'STARTD': '" + fe1 + "', 'ENDD': '" + fe2 + "' }";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_Productos>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "getProductsByLine"
            );
        }


        public static async Task<result_Q_VarY> getVarYBysku(string token, string url, string company, string sku, DateTime fechaInicial, DateTime fechaFinal, string lineaId)
        {
            HttpClient client = Method_Headers(token, url);
            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss");
            var jsonBody = "{ 'COMP': '" + company + "','SKU': '" + sku + "','F1': '" + fe1 + "', 'F2': '" + fe2 + "','WP': '" + lineaId + "'}";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_VarY>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "getVarYBysku"
            );
        }

        public static async Task<result_Q_VarY> getVarXByvarY(string token, string url, string company, string sku, string varY, DateTime fechaInicial, DateTime fechaFinal, string lineaId)
        {
            HttpClient client = Method_Headers(token, url);
            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss");

            var jsonBody = "{ 'COMP': '" + company + "','SKU': '" + sku + "','VARY': '" + varY.Substring(0, 3) + "%','F1': '" + fe1 + "', 'F2': '" + fe2 + "','WP': '" + lineaId + "'}";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_VarY>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "getVarXByvarY"
            );
        }


        public static async Task<result_Q_Resultados> getResultsByVarX(string token, string url, string company, string lineaId, DateTime fechaInicial, DateTime fechaFinal, string variable)
        {
            HttpClient client = Method_Headers(token, url);

            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss");

            var jsonBody = "{ 'COMP': '" + company + "','WP': '" + lineaId + "','COP': '" + variable + "','F1': '" + fe1 + "', 'F2': '" + fe2 + "' }";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_Resultados>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "getResultsByVarX"
            );
        }

        public static IEnumerable<YSummary> BuildYSummaries(
    IEnumerable<YRawRow> rows, DateTime f1, DateTime f2)
        {
            var totalDays = (int)(f2.Date - f1.Date).TotalDays + 1;

            return rows
                .GroupBy(r => new { r.controlOperation, r.controlOperationName })
                .Select(g =>
                {
                    var ordered = g.OrderBy(r => r.executionDate).ToList();
                    var tests = ordered.Count;

                    DateTime? lastTs = null; double? lastVal = null;
                    if (tests > 0)
                    {
                        var last = ordered[^1];
                        lastTs = last.executionDate;
                        lastVal = last.resultValue;
                    }

                    var coverageDays = ordered.Select(r => r.executionDate.Date).Distinct().Count();
                    var oos = ordered.Count(r =>
                        r.resultValue.HasValue &&
                        r.minTolerance.HasValue &&
                        r.maxTolerance.HasValue &&
                        (r.resultValue.Value < r.minTolerance.Value ||
                         r.resultValue.Value > r.maxTolerance.Value));

                    var mean = ordered.Where(r => r.resultValue.HasValue)
                                      .Select(r => r.resultValue!.Value)
                                      .DefaultIfEmpty()
                                      .Average();
                    var byDay = ordered
    .Where(r => r.resultValue.HasValue)                // ← quitamos HasValue de executionDate
    .GroupBy(r => r.executionDate.Date)                // ← sin .Value
    .OrderBy(g => g.Key)
    .Select(g => g.Average(x => x.resultValue!.Value))
    .ToList();

                    static List<double> Downsample(List<double> src, int target)
                    {
                        if (src == null || src.Count <= target) return src ?? new();
                        var step = (double)src.Count / target;
                        var outp = new List<double>(target);
                        for (int i = 0; i < target; i++)
                        {
                            int a = (int)Math.Round(i * step);
                            int b = (int)Math.Round((i + 1) * step);
                            a = Math.Clamp(a, 0, src.Count - 1);
                            b = Math.Clamp(b, 0, src.Count);
                            if (b <= a) b = Math.Min(a + 1, src.Count);
                            var slice = src.GetRange(a, b - a);
                            outp.Add(slice.Average());
                        }
                        return outp;
                    }
                    var spark = Downsample(byDay, 24);

                    return new YSummary
                    {
                        Codigo = (g.Key.controlOperation ?? "").Trim().ToUpper(),
                        Nombre = (g.Key.controlOperationName ?? "").Trim(),
                        Tests = tests,
                        CoverageDays = coverageDays,
                        TotalDays = totalDays,
                        OOS = oos,
                        Mean = double.IsNaN(mean) ? (double?)null : mean,
                        LastTs = lastTs,
                        LastValue = lastVal,
                        Spark = spark
                    };
                })
                .OrderByDescending(s => s.Tests);
        }



        public static async Task<ResultEnvelope<List<YRawRow>>> getVarYRows(
    string token, string url, string company, string sku,
    DateTime fechaInicial, DateTime fechaFinal, string lineaId)
        {
            HttpClient client = Method_Headers(token, url);

            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss");

            var jsonBody =
                "{ 'COMP':'" + company + "'," +
                "  'SKU':'" + sku + "'," +
                "  'F1':'" + fe1 + "'," +
                "  'F2':'" + fe2 + "'," +
                "  'WP':'" + lineaId + "' }";

            // ❗ Usa aquí el nombre de la acción que tengas configurada para tu NUEVO query
            // que devuelve las filas crudas (resultValue, min/maxTolerance, executionDate, etc.)
            // Si tu backend ya lo expone con otro nombre, solo cámbialo aquí.
            return await WebServiceHelper.SafePostAndDeserialize<ResultEnvelope<List<YRawRow>>>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "getVarYRows"
            );
        }

        public static async Task<ResultEnvelope<List<YRawRow>>> getVarXRows(
    string token, string url, string company, string sku,
    string varY, DateTime f1, DateTime f2, string line)
        {
            var client = Method_Headers(token, url);
            var fe1 = f1.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = f2.ToString("yyyy-MM-dd HH:mm:ss");
            var prefix = (varY?.Substring(0, 3) ?? "") + "%";

            var jsonBody = "{ 'COMP':'" + company + "'," +
                           "  'SKU':'" + sku + "'," +
                           "  'F1':'" + fe1 + "'," +
                           "  'F2':'" + fe2 + "'," +
                           "  'WP':'" + line + "'," +
                           "  'VARY':'" + prefix + "' }";

            // Cambia "getVarXRows" por el nombre real de tu procedimiento/endpoint
            return await WebServiceHelper.SafePostAndDeserialize<ResultEnvelope<List<YRawRow>>>(
                client, client.BaseAddress.ToString(), jsonBody, "getVarXRows");
        }

    }
}
