namespace dashboardQ40.Models
{
    
        public record CapabilityRow(
       string Part, string Process, string Test,
       int Subgroups, double Mean, double? MeanDelta,
       double? Cp, double? Cpk, string Nota);

        public record AggRow(
            string Part, string Process, string Test,
            int Subgroups, double Mean, double Rbar, int Nobs,
            int n_min, int n_max, DateTime FirstTs, DateTime LastTs);

        public record SgRow(
            string Part, string Process, string Test,
            DateTime SubgroupTs, int n, double xbar, double R);

        public record Spec(double LSL, double USL, double Target);
    
}
