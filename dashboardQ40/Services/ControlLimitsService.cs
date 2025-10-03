using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static dashboardQ40.Models.ControlLimitsModel;

namespace dashboardQ40.Services
{
    // --- DTO que retorna el repositorio (ver punto 2) ---
    public record AutocontrolSeries(
        string CP,
        string Sku,
        string Variable,
        Guid AutocontrolId,
        double Lsl,
        double Usl,
        IReadOnlyList<double> Values
    );

    public sealed record SeriesForChartDto(
        double[] Values,
        double? Mean,
        double? Sigma,
        double? Lsl,
        double? Usl
    );


    public sealed record TsPointDto(DateTime Timestamp, double Value);


    public sealed class ControlLimitsService
    {
        private readonly IAutocontrolRepository _repo; // lee valores y LSL/USL actuales

        public ControlLimitsService(IAutocontrolRepository repo) => _repo = repo;

        // --- Estadísticos (muestral) ---
        public static (double Mean, double Sigma) Stats(IReadOnlyList<double> values)
        {
            var n = values.Count;
            if (n < 2) throw new InvalidOperationException("Not enough data");
            var mean = values.Average();
            var sumSq = values.Sum(v => Math.Pow(v - mean, 2));
            var sigma = Math.Sqrt(sumSq / (n - 1)); // muestral
            return (mean, sigma);
        }

        public static double Cpk(double mean, double sigma, double lsl, double usl)
        {
            if (sigma <= 0) return double.PositiveInfinity;
            var cpu = (usl - mean) / (3.0 * sigma);
            var cpl = (mean - lsl) / (3.0 * sigma);
            return Math.Min(cpu, cpl);
        }

        // Solo estrecha hacia adentro si CpkActual ≤ minCpk.
        // Nunca expande. Usa un porcentaje de estrechamiento respecto al ancho efectivo.
        private static (double LSLp, double USLp) SuggestLimits(
            double mean,
            double sigma,
            double lslOrig,
            double uslOrig,
            double cpkActual,
            double minCpk,
            double tightenPct = 0.90)  // 0.90 = 10% más estrecho
        {
            // Si el Cpk ya es mejor que el umbral, no estrechamos
            if (cpkActual > minCpk) return (lslOrig, uslOrig);

            if (lslOrig >= uslOrig) return (lslOrig, uslOrig);

            // Anchos efectivos a cada lado de la media
            var left = mean - lslOrig;
            var right = uslOrig - mean;

            // Si la media quedó fuera (valores negativos), no estrechamos
            if (left <= 0 || right <= 0) return (lslOrig, uslOrig);

            // Tomamos el lado limitante para centrar el estrechamiento
            var half = Math.Min(left, right);
            var halfNew = half * tightenPct;   // estrechar p.ej. 10%
            var lslTgt = mean - halfNew;
            var uslTgt = mean + halfNew;

            // Clamping: solo mover hacia adentro
            var lslPrime = Math.Max(lslTgt, lslOrig);
            var uslPrime = Math.Min(uslTgt, uslOrig);

            // Si por asimetría no se logró estrechar, deja como estaba
            if (lslPrime <= lslOrig && uslPrime >= uslOrig)
                return (lslOrig, uslOrig);

            return (lslPrime, uslPrime);
        }

        public static (double LslNew, double UslNew) SuggestLimits(double mean, double sigma, double cpkTarget)
        {
            var halfWidth = 3.0 * cpkTarget * sigma;
            return (mean - halfWidth, mean + halfWidth);
        }

        // --- Listado de candidatos ---
        /*
        public async Task<IReadOnlyList<AutocontrolCandidateDto>> GetCandidatesAsync(
     string token,
     string plantaOrCompany,
     int months = 6,            // ventana semestral por defecto
     int minN = 100,            // N mínimo
     double maxCpk = 1.33,      // Cpk máximo para aparecer en el reporte
     double tightenPct = 0.90,  // 10% más estrecho (si quieres sugerir límites)
     CancellationToken ct = default)
        {
            var periodEnd = DateTime.UtcNow;
            var periodStart = periodEnd.AddMonths(-months);

            var series = await _repo.GetTimeSeriesForPeriodAsync(token, plantaOrCompany, periodStart, periodEnd, ct);
            var list = new List<AutocontrolCandidateDto>();

            foreach (var s in series)
            {
                var values = s.Values ?? Array.Empty<double>();
                if (values.Count < minN) continue;                 // Regla 1: N ≥ 100

                var (mean, sigma) = Stats(values);
                var cpk = Cpk(mean, sigma, s.Lsl, s.Usl);

                if (double.IsNaN(cpk) || cpk > maxCpk) continue;   // Regla 2: Cpk ≤ 1.33

                // (opcional) Proponer límites más estrechos, sin expandir
                var (lslNew, uslNew) = SuggestLimitsOnlyTighten(
                    mean, sigma, s.Lsl, s.Usl, tightenPct);

                list.Add(new AutocontrolCandidateDto(
                    s.Sku,
                    s.Variable,
                    s.AutocontrolId,
                    periodStart,
                    periodEnd,
                    values.Count,
                    mean,
                    sigma,
                    s.Lsl,          // LSL original
                    s.Usl,          // USL original
                    cpk,            // Cpk actual
                    lslNew,         // LSL' sugerido (estrecho)
                    uslNew,         // USL' sugerido (estrecho)
                    true            // puedes ignorarlo en UI
                ));
            }

            // ordena del peor Cpk al menos malo (útil para priorizar)
            return list.OrderBy(x => x.Cpk)
                       .ThenBy(x => x.Sku)
                       .ThenBy(x => x.Variable)
                       .ToList();
        }
        */

        // Nunca expande, solo mueve hacia adentro un % del ancho efectivo respecto a la media.
        private static (double LSLp, double USLp) SuggestLimitsOnlyTighten(
            double mean, double sigma, double lslOrig, double uslOrig, double tightenPct /* 0.90 = 10% */)
        {
            if (lslOrig >= uslOrig) return (lslOrig, uslOrig);

            var left = mean - lslOrig;
            var right = uslOrig - mean;

            if (left <= 0 || right <= 0) return (lslOrig, uslOrig); // media fuera → no tocar

            var half = Math.Min(left, right);
            var halfNew = half * tightenPct;     // estrictamente más estrecho
            var lslTgt = mean - halfNew;
            var uslTgt = mean + halfNew;

            var lslPrime = Math.Max(lslTgt, lslOrig);  // jamás bajar el LSL original
            var uslPrime = Math.Min(uslTgt, uslOrig);  // jamás subir el USL original

            if (lslPrime <= lslOrig && uslPrime >= uslOrig)
                return (lslOrig, uslOrig);

            return (lslPrime, uslPrime);
        }


        // Services/ControlLimitsService.cs (añade este método)
        public async Task<IReadOnlyList<AutocontrolCandidateDto>> GetCandidatesRangeAsync(
    string token, string company, DateTime periodStart, DateTime periodEnd,
    int minN, double maxCpk, CancellationToken ct = default)
        {
            var series = await _repo.GetTimeSeriesForPeriodAsync(token, company, periodStart, periodEnd, ct)
                         ?? Array.Empty<AutocontrolSeries>();

            var list = new List<AutocontrolCandidateDto>();

            foreach (var s in series)
            {
                var values = s.Values ?? Array.Empty<double>();
                if (values.Count < minN) continue; // único filtro

                var (mean, sigma) = Stats(values);
                var cpk = SafeCpk(mean, sigma, s.Lsl, s.Usl);   // ← double?

                double? lslNew = null, uslNew = null;
                if (cpk.HasValue && cpk.Value > maxCpk)
                {
                    var (l, u) = SuggestLimits3Sigma(mean, sigma);
                    if (!double.IsNaN(l) && !double.IsInfinity(l) &&
                        !double.IsNaN(u) && !double.IsInfinity(u))
                    {
                        lslNew = l;
                        uslNew = u;
                    }
                }

                list.Add(new AutocontrolCandidateDto(
                    s.CP,
                    s.Sku, s.Variable, s.AutocontrolId,
                    periodStart, periodEnd, values.Count,
                    mean, sigma, s.Lsl, s.Usl, cpk, lslNew, uslNew, true
                ));
            }

            // Orden: primero los que requieren plan (Cpk == null o <= maxCpk)
            return list
                .OrderBy(x => !(x.CpkCurrent.HasValue && x.CpkCurrent.Value > maxCpk))
                .ThenBy(x => x.CpkCurrent ?? double.NegativeInfinity)
                .ThenBy(x => x.Sku)
                .ThenBy(x => x.Variable)
                .ToList();
        }

        public async Task<SeriesForChartDto> GetSeriesForChartAsync(
         string token, string company, string sku, string variable,
         DateTime periodStart, DateTime periodEnd,
         CancellationToken ct = default)
        {
            var all = await _repo.GetTimeSeriesForPeriodAsync(token, company, periodStart, periodEnd, ct)
                      ?? Array.Empty<AutocontrolSeries>();

            var s = all.FirstOrDefault(x =>
                     string.Equals(x.Sku, sku, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(x.Variable, variable, StringComparison.OrdinalIgnoreCase));

            if (s == null || s.Values == null || s.Values.Count == 0)
                return new SeriesForChartDto(Array.Empty<double>(), null, null, null, null);

            var vals = s.Values.Where(v => double.IsFinite(v)).ToArray();
            if (vals.Length == 0)
                return new SeriesForChartDto(Array.Empty<double>(), null, null, s.Lsl, s.Usl);

            // Usa tu mismo helper Stats(mean, sigma)
            var (mean, sigma) = Stats(vals);

            return new SeriesForChartDto(vals, mean, sigma, s.Lsl, s.Usl);
        }


        private static (double LslNew, double UslNew) SuggestLimits3Sigma(double mean, double sigma)
    => (mean - 3.0 * sigma, mean + 3.0 * sigma);

        private static double? SafeCpk(double mean, double sigma, double lsl, double usl)
        {
            if (sigma <= 0) return null;
            if (lsl >= usl) return null;

            var cpu = (usl - mean) / (3 * sigma);
            var cpl = (mean - lsl) / (3 * sigma);
            var cpk = Math.Min(cpu, cpl);

            if (double.IsNaN(cpk) || double.IsInfinity(cpk)) return null;
            return cpk;
        }

       
    }
}
