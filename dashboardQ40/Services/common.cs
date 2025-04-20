using System.Net.Http.Headers;

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
}
