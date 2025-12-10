using static dashboardQ40.Models.AnalisisSensorialesModel;
using System.Text.RegularExpressions;
using static dashboardQ40.Models.Models;
using static dashboardQ40.Helpers.common;
using System.Text.Json;
using dashboardQ40.Helpers;

namespace dashboardQ40.Services
{
    public class AnalisisSensorialService
    {


        private static readonly Regex ExtraRegex = new Regex(
            @"^(?<tipo>IDMTRA|PROD|LOTE|HORA)#(?<num>\d+)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex OpNumeroRegex = new Regex(
           @"#(?<num>\d+)\s*$",
           RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static int? TryGetNumeroFromOpName(string? opName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(opName))
                    return null;

                var m = OpNumeroRegex.Match(opName);
                if (!m.Success)
                    return null;

                if (int.TryParse(m.Groups["num"].Value, out var n))
                    return n;

                return null;
            }
            catch (RegexMatchTimeoutException ex)
            {
                Console.WriteLine($"[ERROR] Regex timeout in TryGetNumeroFromOpName: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error in TryGetNumeroFromOpName: {ex.Message}");
                return null;
            }
        }

        public static Dictionary<int, LoteExtraPorNumero> MapLoteExtras(
    IEnumerable<BatchExtraRowDto> rows)
        {
            try
            {
                if (rows == null)
                {
                    Console.WriteLine("[WARNING] MapLoteExtras received null rows");
                    return new Dictionary<int, LoteExtraPorNumero>();
                }

                var dict = new Dictionary<int, LoteExtraPorNumero>();

                foreach (var row in rows)
                {
                    try
                    {
                        if (row == null || string.IsNullOrWhiteSpace(row.ExtraField))
                            continue;

                        var m = ExtraRegex.Match(row.ExtraField);
                        if (!m.Success)
                            continue;

                        if (!int.TryParse(m.Groups["num"].Value, out var num))
                        {
                            Console.WriteLine($"[WARNING] Failed to parse number from ExtraField: {row.ExtraField}");
                            continue;
                        }

                        var tipo = m.Groups["tipo"].Value.ToUpperInvariant();

                        if (!dict.TryGetValue(num, out var item))
                        {
                            item = new LoteExtraPorNumero
                            {
                                Numero = num
                            };
                            dict[num] = item;
                        }

                        var val = row.StringValue;

                        switch (tipo)
                        {
                            case "IDMTRA":
                                item.IdMuestra = val;
                                break;
                            case "PROD":
                                item.Producto = val;
                                break;
                            case "LOTE":
                                item.LoteBotella = val;
                                break;
                            case "HORA":
                                item.Hora = val;
                                break;
                        }
                    }
                    catch (RegexMatchTimeoutException ex)
                    {
                        Console.WriteLine($"[ERROR] Regex timeout processing row in MapLoteExtras: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error processing row in MapLoteExtras - ExtraField: {row?.ExtraField}, Error: {ex.Message}");
                        continue;
                    }
                }

                return dict;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Critical error in MapLoteExtras: {ex.Message}");
                return new Dictionary<int, LoteExtraPorNumero>();
            }
        }


        private static readonly Regex NombreCampoRegex = new Regex(
    @"^(?<label>.+?)\s+#(?<num>\d+)$",
    RegexOptions.Compiled);

        public static List<AutoControlPorNumero> MapAutoControl(
    IEnumerable<AutoControlRowDto> rows)
        {
            try
            {
                if (rows == null)
                {
                    Console.WriteLine("[WARNING] MapAutoControl received null rows");
                    return new List<AutoControlPorNumero>();
                }

                // KEY = (CP, Level, Worker, Numero)
                var dict = new Dictionary<(string cp, int lvl, string worker, int num), AutoControlPorNumero>();

                foreach (var row in rows)
                {
                    try
                    {
                        if (row == null || string.IsNullOrWhiteSpace(row.ControlOperationName))
                            continue;

                        var m = NombreCampoRegex.Match(row.ControlOperationName);
                        if (!m.Success)
                            continue;

                        var label = m.Groups["label"].Value.Trim();

                        if (!int.TryParse(m.Groups["num"].Value, out var num))
                        {
                            Console.WriteLine($"[WARNING] Failed to parse number from ControlOperationName: {row.ControlOperationName}");
                            continue;
                        }

                        var key = (row.ControlProcedure, row.ControlProcedureLevel, row.Worker, num);

                        if (!dict.TryGetValue(key, out var item))
                        {
                            item = new AutoControlPorNumero
                            {
                                ControlProcedure = row.ControlProcedure,
                                ControlProcedureLevel = row.ControlProcedureLevel,
                                Worker = row.Worker,
                                WorkerName = row.WorkerName,
                                Numero = num
                            };
                            dict[key] = item;
                        }

                        var val = row.ResultPresetAttributeValue;

                        // Map based on field text
                        if (label.StartsWith("Nombre de producto", StringComparison.OrdinalIgnoreCase))
                        {
                            item.NombreProducto = val;
                        }
                        else if (label.StartsWith("Observaciones Sabor", StringComparison.OrdinalIgnoreCase))
                        {
                            item.ObsSabor = val;
                        }
                        else if (label.StartsWith("Sabor (IN/OUT)", StringComparison.OrdinalIgnoreCase))
                        {
                            item.SaborInOut = ResolverInOut(row);
                        }
                        else if (label.StartsWith("Observaciones Olor", StringComparison.OrdinalIgnoreCase))
                        {
                            item.ObsOlor = val;
                        }
                        else if (label.StartsWith("Olor (IN/OUT)", StringComparison.OrdinalIgnoreCase))
                        {
                            item.OlorInOut = ResolverInOut(row);
                        }
                        else if (label.StartsWith("Apariencia (IN/OUT)", StringComparison.OrdinalIgnoreCase))
                        {
                            item.AparienciaInOut = val;
                        }
                    }
                    catch (RegexMatchTimeoutException ex)
                    {
                        Console.WriteLine($"[ERROR] Regex timeout processing row in MapAutoControl: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error processing row in MapAutoControl - ControlOperationName: {row?.ControlOperationName}, Error: {ex.Message}");
                        continue;
                    }
                }

                return dict.Values.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Critical error in MapAutoControl: {ex.Message}");
                return new List<AutoControlPorNumero>();
            }
        }



        public static List<ReporteSensorialFila> BuildReporteSensorial(
     IEnumerable<BatchExtraRowDto> batchRows,
     IEnumerable<AutoControlRowDto> autoRows)
        {
            try
            {
                if (batchRows == null || autoRows == null)
                {
                    Console.WriteLine("[WARNING] BuildReporteSensorial received null parameters");
                    return new List<ReporteSensorialFila>();
                }

                var batchList = batchRows.ToList();
                if (!batchList.Any())
                {
                    Console.WriteLine("[INFO] BuildReporteSensorial: No batch rows to process");
                    return new List<ReporteSensorialFila>();
                }

                // Common batch data (same in all rows from first query)
                var first = batchList.First();

                var extrasPorNumero = MapLoteExtras(batchList);
                var autoPorNumero = MapAutoControl(autoRows);

                var result = new List<ReporteSensorialFila>();

                foreach (var auto in autoPorNumero)
                {
                    try
                    {
                        extrasPorNumero.TryGetValue(auto.Numero, out var extra);

                        var fila = new ReporteSensorialFila
                        {
                            // Batch data
                            Batch = first.Batch,
                            ManufacturingReference = first.ManufacturingReference,
                            BatchIdentifier = first.BatchIdentifier,
                            ImputationDate = first.ImputationDate,
                            StartDate = first.StartDate,
                            Workplace = first.Workplace,

                            // Panelist
                            ControlProcedure = auto.ControlProcedure,
                            ControlProcedureLevel = auto.ControlProcedureLevel,
                            Worker = auto.Worker,
                            WorkerName = auto.WorkerName,

                            // Number
                            Numero = auto.Numero,

                            // Batch extras
                            IdMuestra = extra?.IdMuestra,
                            ProductoExtra = extra?.Producto,
                            LoteBotella = extra?.LoteBotella,
                            Hora = extra?.Hora,

                            // Sensory results
                            NombreProducto = auto.NombreProducto,
                            ObsSabor = auto.ObsSabor,
                            SaborInOut = auto.SaborInOut,
                            ObsOlor = auto.ObsOlor,
                            OlorInOut = auto.OlorInOut,
                            AparienciaInOut = auto.AparienciaInOut
                        };

                        result.Add(fila);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error creating ReporteSensorialFila for Numero {auto?.Numero}: {ex.Message}");
                        continue;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Critical error in BuildReporteSensorial: {ex.Message}");
                return new List<ReporteSensorialFila>();
            }
        }


        public static async Task<result_Q_BatchExtraRowDto> getLoteAnaSens(
    string token,
    string url,
    string company,
    string trazalog,
    DateTime fechaInicial,
    DateTime fechaFinal)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentException("Token cannot be null or empty", nameof(token));

                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("URL cannot be null or empty", nameof(url));

                if (string.IsNullOrWhiteSpace(company))
                    throw new ArgumentException("Company cannot be null or empty", nameof(company));

                HttpClient client = Method_Headers(token, url);

                var requestBody = new
                {
                    COMP = company,
                    F1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss"),
                    F2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss"),
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);

                return await WebServiceHelper.SafePostAndDeserialize<result_Q_BatchExtraRowDto>(
                    client,
                    client.BaseAddress.ToString(),
                    jsonBody,
                    "QueryanasensDEL",
                    trazalog
                );
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"[ERROR] Invalid argument in getLoteAnaSens: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ERROR] HTTP request failed in getLoteAnaSens: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ERROR] JSON serialization error in getLoteAnaSens: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error in getLoteAnaSens: {ex.Message}");
                throw;
            }
        }

        public static async Task<result_Q_AutoControlRowDto> getACsAnaSens(
    string token,
    string url,
    string company,
    string trazalog,
    int batch)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentException("Token cannot be null or empty", nameof(token));

                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("URL cannot be null or empty", nameof(url));

                if (string.IsNullOrWhiteSpace(company))
                    throw new ArgumentException("Company cannot be null or empty", nameof(company));

                if (batch <= 0)
                    throw new ArgumentException("Batch must be greater than zero", nameof(batch));

                HttpClient client = Method_Headers(token, url);

                var requestBody = new
                {
                    COMP = company,
                    BATCH = batch
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);

                return await WebServiceHelper.SafePostAndDeserialize<result_Q_AutoControlRowDto>(
                    client,
                    client.BaseAddress.ToString(),
                    jsonBody,
                    "QueryanasensACs",
                    trazalog
                );
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"[ERROR] Invalid argument in getACsAnaSens: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ERROR] HTTP request failed in getACsAnaSens: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ERROR] JSON serialization error in getACsAnaSens: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error in getACsAnaSens: {ex.Message}");
                throw;
            }
        }


        public static async Task<result_Q_BatchExtraRowDto> getLoteAnaSensbyCode(
    string token,
    string url,
    string company,
    string trazalog,
    string productCode,
    string productHour)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                    throw new ArgumentException("Token cannot be null or empty", nameof(token));

                if (string.IsNullOrWhiteSpace(url))
                    throw new ArgumentException("URL cannot be null or empty", nameof(url));

                if (string.IsNullOrWhiteSpace(company))
                    throw new ArgumentException("Company cannot be null or empty", nameof(company));

                if (string.IsNullOrWhiteSpace(productCode))
                    throw new ArgumentException("Product code cannot be null or empty", nameof(productCode));

                if (string.IsNullOrWhiteSpace(productHour))
                    throw new ArgumentException("Product hour cannot be null or empty", nameof(productHour));

                HttpClient client = Method_Headers(token, url);

                var requestBody = new
                {
                    COMP = company,
                    CODE = productCode,
                    HR = productHour
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);

                return await WebServiceHelper.SafePostAndDeserialize<result_Q_BatchExtraRowDto>(
                    client,
                    client.BaseAddress.ToString(),
                    jsonBody,
                    "QueryanasensDEL",
                    trazalog
                );
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"[ERROR] Invalid argument in getLoteAnaSensbyCode: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[ERROR] HTTP request failed in getLoteAnaSensbyCode: {ex.Message}");
                throw;
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[ERROR] JSON serialization error in getLoteAnaSensbyCode: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Unexpected error in getLoteAnaSensbyCode: {ex.Message}");
                throw;
            }
        }


        private static string ResolverInOut(AutoControlRowDto ac)
        {
            if (ac == null) return null;

            var preset = ac.ResultPresetAttributeValue?.Trim();
            if (!string.IsNullOrEmpty(preset))
            {
                var txt = preset.ToUpperInvariant();
                if (txt is "IN" or "OUT")
                    return txt;
            }

            if (ac.ControlOperationType == 2 && ac.ResultAttribute is int ra)
                return ra == 1 ? "IN" : "OUT";

            return null;
        }


    }
}
