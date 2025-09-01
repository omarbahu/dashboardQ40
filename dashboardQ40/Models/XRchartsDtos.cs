namespace dashboardQ40.Models
{
    public record VariableOption(string ControlOperation, string ControlOperationName);

    public record XRBaseRow(
        string ControlOperation,
        string ControlOperationName,
        string IdControlProcedureResult,
        string SubgroupId,
        DateTime ExecutionDate,
        double ResultValue,
        double? LSL,
        double? USL,
        double? Target,
        string SpecType,
        int SubgroupN
    );

    public record SubgroupStat(
        string SubgroupId,
        int N,
        double Xbar,
        double? S
    );

    public record CapabilityResult(
        string ControlOperation,
        string ControlOperationName,
        int Npoints,
        int Nsubgroups,
        double MeanAll,
        double? SigmaOverall,
        double? SigmaWithin,
        double? LSL,
        double? USL,
        string SpecType,
        double? Cp,
        double? Cpk,
        double? Pp,
        double? Ppk
    );
}
