using static dashboardQ40.Models.Models;
using System.Text.Json;
using dashboardQ40.Models;
using System.Globalization;
namespace dashboardQ40.Services
{
    public class Helpers
    {
        public static class PermisosHelper
        {
            private const string SessionKey = "permisosDashboard";

            public static List<DashboardProgramPermission> GetPermisos(HttpContext httpContext)
            {
                var json = httpContext.Session.GetString(SessionKey);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<DashboardProgramPermission>();

                return JsonSerializer.Deserialize<List<DashboardProgramPermission>>(json)
                       ?? new List<DashboardProgramPermission>();
            }

            public static DashboardProgramPermission? GetPermisoModulo(HttpContext httpContext, string programGroupName)
            {
                var permisos = GetPermisos(httpContext);

                return permisos
                    .FirstOrDefault(p => string.Equals(
                        p.ProgramGroupName,
                        programGroupName,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        public static CertificadoCaracteristicaDto BuildCertificadoRow(
    string codigo,
    IList<result_Resultados> rows)
        {
            if (rows == null || rows.Count == 0)
                return null;

            // Lecturas válidas
            var valores = rows
                .Where(r => r.resultValue.HasValue)
                .Select(r => Convert.ToDouble(r.resultValue.Value))
                .ToList();

            if (valores.Count == 0)
                return null;

            int n = valores.Count;
            double media = valores.Average();

            // Desviación estándar muestral
            double? sigma = null;
            if (n > 1)
            {
                var sumSq = valores.Sum(v => Math.Pow(v - media, 2));
                sigma = Math.Sqrt(sumSq / (n - 1));
            }

            // LSL / USL del primer registro que los tenga
            double? lsl = rows.Select(r => r.minTolerance)
                              .FirstOrDefault(v => v.HasValue);
            double? usl = rows.Select(r => r.maxTolerance)
                              .FirstOrDefault(v => v.HasValue);

            // Nombre amigable
            var nombre = rows
                .Select(r => r.controlOperationName)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? codigo;

            // % bajo LEI y % sobre LES
            decimal? pctLow = null, pctHigh = null;
            if (n > 0)
            {
                if (lsl.HasValue)
                {
                    int countLow = rows.Count(r =>
                        r.resultValue.HasValue && r.resultValue.Value < lsl.Value);
                    pctLow = (decimal)countLow * 100m / n;
                }
                if (usl.HasValue)
                {
                    int countHigh = rows.Count(r =>
                        r.resultValue.HasValue && r.resultValue.Value > usl.Value);
                    pctHigh = (decimal)countHigh * 100m / n;
                }
            }

            // Cpk
            decimal? cpk = null;
            if (sigma.HasValue && sigma.Value > 0 && lsl.HasValue && usl.HasValue)
            {
                var cpu = (usl.Value - media) / (3.0 * sigma.Value);
                var cpl = (media - lsl.Value) / (3.0 * sigma.Value);
                var cpkDouble = Math.Min(cpu, cpl);
                cpk = (decimal)cpkDouble;
            }

            return new CertificadoCaracteristicaDto
            {
                Nombre = nombre,
                Muestras = n,
                LEI = lsl.HasValue ? (decimal?)lsl.Value : null,
                LES = usl.HasValue ? (decimal?)usl.Value : null,
                Media = (decimal?)media,
                Sigma = sigma.HasValue ? (decimal?)sigma.Value : null,
                PorcBajoLEI = pctLow,
                PorcSobreLES = pctHigh,
                Cpk = cpk
            };
        }

        public static int? ParseTamanoLote(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            var parts = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out var n))
                return n;

            return null;
        }

        private static decimal? ParseNullableDecimal(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            // Intenta parsear con punto o coma dependiendo del Excel
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out d))
                return d;

            return null; // si de plano es basura, mejor null
        }

        public static CertificadoCaracteristicaDto FromCapabilityRow(CapabilityRowDto c)
        {
            if (c == null) return null;

            return new CertificadoCaracteristicaDto
            {
                Nombre = c.VariableName,
                Muestras = c.N,
                LEI = (decimal?)c.LSL,
                LES = (decimal?)c.USL,
                Media = (decimal?)c.Mean,
                Sigma = (decimal?)c.SigmaGlobal,
                PorcBajoLEI = (decimal?)c.PctBelowLsl,
                PorcSobreLES = (decimal?)c.PctAboveUsl,
                Cpk = (decimal?)c.Cpk
            };
        }

        // Dejamos también el cálculo desde lecturas crudas como fallback:
        public static CertificadoCaracteristicaDto BuildCertificadoRowFromRaw(
            string codigo,
            IList<result_Resultados> rows)
        {
            // 👉 aquí básicamente lo que ya tienes en tu BuildCertificadoRow actual,
            //     sólo que devolviendo CertificadoCaracteristicaDto.
            // No lo reescribo completo para no pisarte nada; sólo muévete a devolver
            // CertificadoCaracteristicaDto en vez de otro tipo.
            throw new NotImplementedException();
        }

    }
}
