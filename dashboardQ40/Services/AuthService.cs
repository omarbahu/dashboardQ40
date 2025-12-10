using dashboardQ40.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using static dashboardQ40.Models.Models;
using static dashboardQ40.Helpers.common;

namespace dashboardQ40.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AuthService> _logger;
        private readonly WebServiceSettings _settings;
        private result_token _cachedToken;
        private DateTime _tokenExpiration;

        public AuthService(HttpClient httpClient, ILogger<AuthService> logger, IOptions<WebServiceSettings> settings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value; // Extrae el valor de IOptions
        }

        public async Task<result_token> ObtenerTokenCaptor(string company)
        {
            if (_cachedToken != null && DateTime.UtcNow < _tokenExpiration)
            {
                _logger.LogInformation("Usando token en caché.");
                return _cachedToken;
            }

            try
            {
                _logger.LogInformation("Iniciando el proceso para obtener el token.");

                var cred_tok = new credenciales_token
                {
                    userName = _settings.UserNameWS,
                    password = _settings.PasswordWS
                };

                var data = JsonSerializer.Serialize(cred_tok);
                HttpContent content = new StringContent(data, System.Text.Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync(_settings.BaseUrl + _settings.TokenUrl + company, content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var resultData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var tokenResponse = JsonSerializer.Deserialize<result_token>(resultData);

                    if (tokenResponse != null)
                    {
                        _cachedToken = tokenResponse;
                        _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60); // Restamos 1 min

                        _logger.LogInformation("Token obtenido y almacenado en caché.");
                        return tokenResponse;
                    }
                }

                _logger.LogWarning("La solicitud de token falló con código de estado: {StatusCode}", response.StatusCode);
                throw new HttpRequestException($"Error al obtener el token. Código de estado: {response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error de conexión al intentar obtener el token.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error inesperado al intentar obtener el token.");
                throw;
            }
        }


        public static async Task<result_Q_authUser> getAuthUser(string token, string url, string company, string appuser)
        {
            var result = new result_Q_authUser();

            try
            {
                HttpClient client = Method_Headers(token, url);

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeUriString(client.BaseAddress.ToString()));
                //var data = "{ 'COMP': '" + company + "' , 'APPUSER': '" + appuser + "' }";

                var requestBody = new
                {
                    COMP = company,
                    APPUSER = appuser
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);

                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage tokenResponse = await client.PostAsync(Uri.EscapeUriString(client.BaseAddress.ToString()), request.Content);
                
                if (tokenResponse.IsSuccessStatusCode)
                {
                    var resultData = await tokenResponse.Content.ReadAsStringAsync();
                    
                    result = JsonSerializer.Deserialize<result_Q_authUser>(resultData, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                //_logger.LogInformation(ex, "Error en crear solicitud: " + ex.ToString());

                throw;
            }
        }
    }
}
