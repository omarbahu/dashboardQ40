using static dashboardQ40.Models.AnalisisSensorialesModel;
using System.Text.RegularExpressions;
using static dashboardQ40.Models.Models;
using static dashboardQ40.Services.common;

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
            if (string.IsNullOrWhiteSpace(opName))
                return null;

            var m = OpNumeroRegex.Match(opName);
            if (!m.Success)
                return null;

            if (int.TryParse(m.Groups["num"].Value, out var n))
                return n;

            return null;
        }

        public static Dictionary<int, LoteExtraPorNumero> MapLoteExtras(
            IEnumerable<BatchExtraRowDto> rows)
        {
            var dict = new Dictionary<int, LoteExtraPorNumero>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.ExtraField))
                    continue;

                var m = ExtraRegex.Match(row.ExtraField);
                if (!m.Success)
                    continue;

                var num = int.Parse(m.Groups["num"].Value);
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

            return dict;
        }

        private static readonly Regex NombreCampoRegex = new Regex(
    @"^(?<label>.+?)\s+#(?<num>\d+)$",
    RegexOptions.Compiled);

        public static List<AutoControlPorNumero> MapAutoControl(
    IEnumerable<AutoControlRowDto> rows)
        {
            // KEY = (CP, Level, Worker, Numero)
            var dict = new Dictionary<(string cp, int lvl, string worker, int num), AutoControlPorNumero>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.ControlOperationName))
                    continue;

                var m = NombreCampoRegex.Match(row.ControlOperationName);
                if (!m.Success)
                    continue;

                var label = m.Groups["label"].Value.Trim();
                var num = int.Parse(m.Groups["num"].Value);

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

                // Mapeo según el texto del campo
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
                    // 🔹 AQUÍ usamos la lógica nueva
                    item.SaborInOut = ResolverInOut(row);
                }
                else if (label.StartsWith("Observaciones Olor", StringComparison.OrdinalIgnoreCase))
                {
                    item.ObsOlor = val;
                }
                else if (label.StartsWith("Olor (IN/OUT)", StringComparison.OrdinalIgnoreCase))
                {
                    // 🔹 AQUÍ también
                    item.OlorInOut = ResolverInOut(row);
                }
                else if (label.StartsWith("Apariencia (IN/OUT)", StringComparison.OrdinalIgnoreCase))
                {
                    // Opcional:
                    // - si quieres misma lógica que sabor/olor:
                    // item.AparienciaInOut = ResolverInOut(row);
                    // - si apariencia siempre viene como texto "IN/OUT" en el preset, deja así:
                    item.AparienciaInOut = val;
                }
            }

            return dict.Values.ToList();
        }



        public static List<ReporteSensorialFila> BuildReporteSensorial(
    IEnumerable<BatchExtraRowDto> batchRows,
    IEnumerable<AutoControlRowDto> autoRows)
        {
            var batchList = batchRows.ToList();
            if (!batchList.Any())
                return new List<ReporteSensorialFila>();

            // Datos comunes del batch (son iguales en todas las filas del primer query)
            var first = batchList.First();

            var extrasPorNumero = MapLoteExtras(batchList);
            var autoPorNumero = MapAutoControl(autoRows);

            var result = new List<ReporteSensorialFila>();

            foreach (var auto in autoPorNumero)
            {
                extrasPorNumero.TryGetValue(auto.Numero, out var extra);

                var fila = new ReporteSensorialFila
                {
                    // Datos de batch
                    Batch = first.Batch,
                    ManufacturingReference = first.ManufacturingReference,
                    BatchIdentifier = first.BatchIdentifier,
                    ImputationDate = first.ImputationDate,
                    StartDate = first.StartDate,
                    Workplace = first.Workplace,

                    // Panelista
                    ControlProcedure = auto.ControlProcedure,
                    ControlProcedureLevel = auto.ControlProcedureLevel,
                    Worker = auto.Worker,
                    WorkerName = auto.WorkerName,

                    // Número
                    Numero = auto.Numero,

                    // Extra de lote
                    IdMuestra = extra?.IdMuestra,
                    ProductoExtra = extra?.Producto,
                    LoteBotella = extra?.LoteBotella,
                    Hora = extra?.Hora,

                    // Resultado sensorial
                    NombreProducto = auto.NombreProducto,
                    ObsSabor = auto.ObsSabor,
                    SaborInOut = auto.SaborInOut,
                    ObsOlor = auto.ObsOlor,
                    OlorInOut = auto.OlorInOut,
                    AparienciaInOut = auto.AparienciaInOut
                };

                result.Add(fila);
            }

            return result;
        }


        public static async Task<result_Q_BatchExtraRowDto> getLoteAnaSens(
     string token,
     string url,
     string company,
     string trazalog,
     DateTime fechaInicial,
     DateTime fechaFinal)
        {
            HttpClient client = Method_Headers(token, url);

            var fe1 = fechaInicial.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = fechaFinal.ToString("yyyy-MM-dd HH:mm:ss");

            // Param names = como los declaraste en el query en Captor
            var jsonBody =
                "{ 'COMP': '" + company + "'" +
                ", 'F1': '" + fe1 + "'" +
                ", 'F2': '" + fe2 + "'" +
                "}";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_BatchExtraRowDto>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "QueryanasensDEL",
                trazalog
            );
        }

        public static async Task<result_Q_AutoControlRowDto> getACsAnaSens(
            string token,
            string url,
            string company,
            string trazalog,
            int batch)
        {
            HttpClient client = Method_Headers(token, url);

            var jsonBody =
                "{ 'COMP': '" + company + "'" +
                ", 'BATCH': '" + batch + "'" +
                "}";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_AutoControlRowDto>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "QueryanasensACs",
                trazalog
            );
        }


        public static async Task<result_Q_BatchExtraRowDto> getLoteAnaSensbyCode(
    string token,
    string url,
    string company,
    string trazalog,
    string productCode, string productHour)
        {
            HttpClient client = Method_Headers(token, url);


            // Param names = como los declaraste en el query en Captor
            var jsonBody =
                "{ 'COMP': '" + company + "'" +
                ", 'CODE': '" + productCode + "'" +
                ", 'HR': '" + productHour + "'" +
                "}";

            return await WebServiceHelper.SafePostAndDeserialize<result_Q_BatchExtraRowDto>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                "QueryanasensDEL",
                trazalog
            );
        }


        private static string ResolverInOut(AutoControlRowDto ac)
        {
            if (ac == null) return null;

            // 1) Si ya viene texto IN / OUT, respétalo
            if (!string.IsNullOrWhiteSpace(ac.ResultPresetAttributeValue))
            {
                var txt = ac.ResultPresetAttributeValue.Trim().ToUpperInvariant();
                if (txt == "IN" || txt == "OUT")
                    return txt;
            }

            // 2) Si es operación tipo IN/OUT (type = 2), usar ResultAttribute
            if (ac.ControlOperationType == 2 && ac.ResultAttribute.HasValue)
            {
                return ac.ResultAttribute.Value == 1 ? "IN" : "OUT";
            }

            return null;
        }

    }
}
