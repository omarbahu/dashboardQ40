using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security;
using System;

namespace dashboardQ40.Services
{
    using System.Xml.Linq;
    using System.Security;

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
            request.Headers.Add("SOAPAction", soapAction);

            var response = await client.SendAsync(request);
            string responseXml = await response.Content.ReadAsStringAsync();

            // 1) Error HTTP (timeout, 500, etc.)
            if (!response.IsSuccessStatusCode)
                throw new Exception($"HTTP {(int)response.StatusCode}: {responseXml}");

            // 2) Parsear XML y buscar Fault / mensajes de error
            try
            {
                var doc = XDocument.Parse(responseXml);

                XNamespace soapEnv = "http://schemas.xmlsoap.org/soap/envelope/";
                var fault = doc.Descendants(soapEnv + "Fault").FirstOrDefault();
                if (fault != null)
                {
                    var faultString = fault.Element("faultstring")?.Value
                                      ?? fault.Element("faultcode")?.Value
                                      ?? fault.ToString();
                    throw new Exception($"SOAP Fault: {faultString}");
                }

                // (Opcional) Buscar nodos de error tipo <ErrorMessage>, <ErrorDescription>, etc.
                var errorNode = doc
                    .Descendants()
                    .FirstOrDefault(x =>
                        x.Name.LocalName.Contains("Error", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(x.Value));

                if (errorNode != null)
                {
                    throw new Exception($"SOAP Error: {errorNode.Value}");
                }
            }
            catch (Exception ex) when (ex is not Exception)
            {
                // Si falla el parseo, no tumbamos el flujo por ello; devolvemos el XML tal cual.
                // (pero en la mayoría de los casos sí entra al catch de arriba con mensaje claro)
            }

            // Si llegamos aquí, asumimos que fue OK
            return responseXml;
        }
    }

}
