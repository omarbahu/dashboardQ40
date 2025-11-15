using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System;

namespace dashboardQ40.Services
{
    public static class CaptorPerformActionsService
    {
        public static async Task<string> CompleteControlProcedureAsync(
         string url,
         string soapNamespace,
         string soapAction,
         string company,
         string user,
         string jsonPayload)
        {
            string escapedJson = SecurityElement.Escape(jsonPayload);

            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema""
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <CompleteControlProcedure xmlns=""{soapNamespace}"">
      <company>{SecurityElement.Escape(company)}</company>
      <user>{SecurityElement.Escape(user)}</user>
      <controlProcedureResult>{escapedJson}</controlProcedureResult>
    </CompleteControlProcedure>
  </soap:Body>
</soap:Envelope>";

            using var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

            // IMPORTANTE: usar exactamente el SOAPAction de la página
            request.Headers.Add("SOAPAction", soapAction);

            var response = await client.SendAsync(request);
            string responseXml = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"HTTP {response.StatusCode}: {responseXml}");

            return responseXml;
        }
    }
}
