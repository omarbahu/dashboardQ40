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
            var result = new result_Q_Lineas();

            try
            {
                HttpClient client = Method_Headers(token, url);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeUriString(client.BaseAddress.ToString()));
                var data = "{ 'COMP': '" + company + "' }";
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage tokenResponse = await client.PostAsync(Uri.EscapeUriString(client.BaseAddress.ToString()), request.Content);
                if (trazalog == "1") // si esta activado el track del log, guardamos datos
                {
                    //_logger.LogInformation("status code solicitudes: " + tokenResponse.StatusCode.ToString() + " tokenResponse: " + tokenResponse.ToString());
                   
                }
                if (tokenResponse.IsSuccessStatusCode)
                {
                    var resultData = await tokenResponse.Content.ReadAsStringAsync();
                    if (trazalog == "1") // si esta activado el track del log, guardamos datos
                    {
                        //_logger.LogInformation("resultData: " + resultData.ToString());

                    }
                    result = JsonSerializer.Deserialize<result_Q_Lineas>(resultData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                //_logger.LogInformation(ex, "Error en crear solicitud: " + ex.ToString());

                throw ex;
            }
        }


        public static async Task<result_Q_Productos> getProductsByLine(string token, string url, string company, string lineaId, DateTime fechaInicial, DateTime fechaFinal)
        {
            var result = new result_Q_Productos();
            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss"); 
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss"); 
            try
            {
                HttpClient client = Method_Headers(token, url);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeUriString(client.BaseAddress.ToString()));
                var data = "{ 'COMP': '" + company + "','WP': '" + lineaId + "', 'STARTD': '" + fe1 + "', 'ENDD': '" + fe2 + "' }";
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage tokenResponse = await client.PostAsync(Uri.EscapeUriString(client.BaseAddress.ToString()), request.Content);
                
                if (tokenResponse.IsSuccessStatusCode)
                {
                    var resultData = await tokenResponse.Content.ReadAsStringAsync();
                    
                    result = JsonSerializer.Deserialize<result_Q_Productos>(resultData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                //_logger.LogInformation(ex, "Error en crear solicitud: " + ex.ToString());

                throw ex;
            }
        }


        public static async Task<result_Q_VarY> getVarYBysku(string token, string url, string company, string sku)
        {
            var result = new result_Q_VarY();

            try
            {
                HttpClient client = Method_Headers(token, url);
                Log.Information("getVarYBysku Token: " + token);
                Log.Information(" getVarYBysku url: " + url);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeUriString(client.BaseAddress.ToString()));
                var data = "{ 'COMP': '" + company + "','SKU': '" + sku + "'}";
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage tokenResponse = await client.PostAsync(Uri.EscapeUriString(client.BaseAddress.ToString()), request.Content);
                Log.Information(" getVarYBysku data: " + data);
                if (tokenResponse.IsSuccessStatusCode)
                {
                    var resultData = await tokenResponse.Content.ReadAsStringAsync();

                    result = JsonSerializer.Deserialize<result_Q_VarY>(resultData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                //_logger.LogInformation(ex, "Error en crear solicitud: " + ex.ToString());

                throw ex;
            }
        }

        public static async Task<result_Q_VarY> getVarXByvarY(string token, string url, string company, string sku, string varY)
        {
            var result = new result_Q_VarY();

            try
            {
                HttpClient client = Method_Headers(token, url);
                Log.Information("getVarXByvarY Token: " + token);
                Log.Information(" getVarXByvarY url: " + url + " vary: " + varY.ToString());
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeUriString(client.BaseAddress.ToString()));
                //var data = "{ 'COMP': '" + company + "','SKU': '" + sku + "'}";
                var data = "{ 'COMP': '" + company + "','SKU': '" + sku + "','VARY': '" + varY.Substring(0, 3) + "%'}";
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage tokenResponse = await client.PostAsync(Uri.EscapeUriString(client.BaseAddress.ToString()), request.Content);
                Log.Information(" getVarXByvarY data: " + data);

                if (tokenResponse.IsSuccessStatusCode)
                {
                    var resultData = await tokenResponse.Content.ReadAsStringAsync();

                    result = JsonSerializer.Deserialize<result_Q_VarY>(resultData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                //_logger.LogInformation(ex, "Error en crear solicitud: " + ex.ToString());

                throw ex;
            }
        }


        public static async Task<result_Q_Resultados> getResultsByVarX(string token, string url, string company, string lineaId, DateTime fechaInicial, DateTime fechaFinal, string variable)
        {
            var result = new result_Q_Resultados();
            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss");
            try
            {
                HttpClient client = Method_Headers(token, url);
                Log.Information("getResultsByVarX Token: " + token);
                Log.Information(" getResultsByVarX url: " + url);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeUriString(client.BaseAddress.ToString()));
                var data = "{ 'COMP': '" + company + "','WP': '" + lineaId + "','COP': '" + variable + "','F1': '" + fe1 + "', 'F2': '" + fe2 + "' }";
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage tokenResponse = await client.PostAsync(Uri.EscapeUriString(client.BaseAddress.ToString()), request.Content);
                Log.Information(" getResultsByVarX data: " + data);
                if (tokenResponse.IsSuccessStatusCode)
                {
                    var resultData = await tokenResponse.Content.ReadAsStringAsync();

                    result = JsonSerializer.Deserialize<result_Q_Resultados>(resultData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                //_logger.LogInformation(ex, "Error en crear solicitud: " + ex.ToString());

                throw ex;
            }
        }
    }
}
