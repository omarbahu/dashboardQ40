// Services/WSControlLimitsRepository.cs
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using static dashboardQ40.Services.common; // Method_Headers, WebServiceHelper
using dashboardQ40.Models;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Services
{
    internal sealed class RawNormRow
    {
        public string? controlOperation { get; set; }
        public string? controlOperationName { get; set; }
        public double? resultValue { get; set; }
        public double? minTolerance { get; set; }
        public double? maxTolerance { get; set; }
        public DateTime? executionDate { get; set; }
        public string? manufacturingReference { get; set; } // SKU
    }


    public sealed class WSControlLimitsRepository : IAutocontrolRepository
    {
        private readonly WebServiceSettings _settings;

        public WSControlLimitsRepository(IOptions<WebServiceSettings> settings)
            => _settings = settings.Value;

        public async Task<IReadOnlyList<AutocontrolSeries>> GetTimeSeriesForPeriodAsync(
            string token, string company, DateTime start, DateTime end, CancellationToken ct = default)
        {
            // Tu patrón: base + sufijo por compañía (igual que QueryLineas, QueryVarY, etc.)
            var client = Method_Headers(token, _settings.BaseUrl + _settings.QueryLimits + company);

            var fe1 = start.ToString("yyyy-MM-dd HH:mm:ss");
            var fe2 = end.ToString("yyyy-MM-dd HH:mm:ss");

            var body = "{ 'COMP':'" + company + "', 'F1':'" + fe1 + "', 'F2':'" + fe2 + "' }";

            var env = await WebServiceHelper.SafePostAndDeserialize<result_QResNormLi>(
              client,
              client.BaseAddress!.ToString(),
              body,
              "QResNormLi",         // ← la MISMA operación que en Postman
              "ControlLimits.QResNormLi"   // (opcional) trazalog
          );

            var rows = env?.result ?? new List<raw_QResNormLi>();

            var groups = rows
     .Where(r => !string.IsNullOrWhiteSpace(r.manufacturingReference)
              && !string.IsNullOrWhiteSpace(r.controlOperation))
     .GroupBy(r => new {
         CP = r.controlprocedure!.Trim(),
         Sku = r.manufacturingReference!.Trim(),
         Op = r.controlOperationName!.Trim()
     });

            var list = new List<AutocontrolSeries>();
            foreach (var g in groups)
            {
                var values = g.Where(x => x.resultValue.HasValue)
                              .OrderBy(x => x.executionDate)
                              .Select(x => x.resultValue!.Value)
                              .ToList();

                var lastWithLimits = g.Where(x => x.minTolerance.HasValue || x.maxTolerance.HasValue)
                                      .OrderBy(x => x.executionDate)
                                      .LastOrDefault();

                double lsl = lastWithLimits?.minTolerance ?? 0;
                double usl = lastWithLimits?.maxTolerance ?? 0;

                var autoId = CreateStableGuid($"{company}|{g.Key.Sku}|{g.Key.Op}");

                list.Add(new AutocontrolSeries(
                    g.Key.CP,
                    g.Key.Sku,
                    g.Key.Op,
                    autoId,
                    lsl,
                    usl,
                    values
                ));
            }

            return list;
        }

        private static Guid CreateStableGuid(string input)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            Span<byte> buf = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(buf);
            return new Guid(buf);
        }
    }
}
