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


        public static async Task<result_Q_VarY> getVarYBysku(string token, string url, string company, string sku)
        {
            HttpClient client = Method_Headers(token, url);

            var jsonBody = "{ 'COMP': '" + company + "','SKU': '" + sku + "'}";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_VarY>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "getVarYBysku"
            );
        }

        public static async Task<result_Q_VarY> getVarXByvarY(string token, string url, string company, string sku, string varY)
        {
            HttpClient client = Method_Headers(token, url);

            var jsonBody = "{ 'COMP': '" + company + "','SKU': '" + sku + "','VARY': '" + varY.Substring(0, 3) + "%'}";

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
    }
}
