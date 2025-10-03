namespace dashboardQ40.Models
{
    public class ControlLimitsModel
    {
        public sealed record AutocontrolCandidateDto(
            string CP,
    string Sku,
    string Variable,
    Guid AutocontrolId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int N,
    double Mean,
    double Sigma,
    double LslCurrent,
    double UslCurrent,
    double? CpkCurrent,      // ← antes double
    double? LslNew,          // ← antes double
    double? UslNew,          // ← antes double
    bool Eligible
);


        public record ApplyRequestItem(
            Guid AutocontrolId,
            string Sku,
            string Variable,
            double LslNew,
            double UslNew,
            string? Notes
        );

        public record ApplyResultItem(
            Guid AutocontrolId,
            string Sku,
            string Variable,
            bool Success,
            string? NewAutocontrolId, // devuelto por Captor al clonar
            string? Error
        );

        public record ApplyBatchResult(
            int Total, int Succeeded, int Failed, List<ApplyResultItem> Items
        );

        public class ControlLimitsDefaults
        {
            public int Months { get; set; } = 6;
            public int MinN { get; set; } = 100;
            public double MinCpk { get; set; } = 1.33;
            public double CpkTarget { get; set; } = 1.40;
        }

        public class ControlLimitsWsOptions
        {
            public string BaseUrl { get; set; } = "";
            public string NormsQueryPath { get; set; } = "";
            public string ValuesQueryPath { get; set; } = "";
            public int TimeoutSeconds { get; set; } = 30;
        }

        public sealed class RawNormRow
        {
            public string? controlOperation { get; set; }
            public string? controlOperationName { get; set; }
            public double? resultValue { get; set; }
            public double? minTolerance { get; set; }
            public double? maxTolerance { get; set; }
            public DateTime? executionDate { get; set; }
            public string? manufacturingReference { get; set; } // ← SKU
        }
    }

}
