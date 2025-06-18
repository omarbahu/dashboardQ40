using Serilog;
using System.Net.Http.Headers;
using System.Text.Json;

namespace dashboardQ40.Services
{
    public class common
    {
        public static HttpClient Method_Headers(string accessToken, string endpointURL)
        {
            HttpClientHandler handler = new HttpClientHandler() { UseDefaultCredentials = false };
            HttpClient client = new HttpClient(handler);

            try
            {
                client.BaseAddress = new Uri(endpointURL);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return client;
        }
    }

    public static class WebServiceHelper
    {
        public static async Task<T> SafePostAndDeserialize<T>(HttpClient client, string url, string jsonBody, string contextTag = "", string trazalog = "0") where T : new()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeUriString(url))
            {
                Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
            };

            try
            {
                HttpResponseMessage response = await client.SendAsync(request);

                if (trazalog == "1")
                    Log.Information($"[{contextTag}] Código de respuesta: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning($"[{contextTag}] Error HTTP {response.StatusCode} desde {url}");
                    return new T(); // objeto vacío del tipo solicitado
                }

                var resultData = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(resultData))
                {
                    Log.Warning($"[{contextTag}] Respuesta vacía desde {url}");
                    return new T();
                }

                return JsonSerializer.Deserialize<T>(resultData, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new T();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"[{contextTag}] Excepción al consumir {url}");
                return new T(); // error controlado
            }
        }
    }
}
