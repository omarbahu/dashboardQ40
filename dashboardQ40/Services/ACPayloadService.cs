// Services/ACPayloadService.cs
using System.Data.SqlClient;
using Dapper;
using static dashboardQ40.Models.Models; // si no usas Dapper dime y lo hacemos con SqlCommand

public interface IACPayloadService
{
    Task<List<ACPayloadRow>> ObtenerRowsAsync(
        IDictionary<string, object> parametros,
        string connectionString);
}

public class ACPayloadService : IACPayloadService
{
    // Aquí pegas tu query completo, con saltos de línea
    private const string SqlACPayloadRows = @"
;WITH CPO_Latest AS (
    SELECT
        CPO.*,
        ROW_NUMBER() OVER (
            PARTITION BY CPO.company, CPO.controlProcedure, CPO.controlOperation
            ORDER BY CPO.controlProcedureVersion DESC, CPO.controlProcedureLevel DESC
        ) AS rn
    FROM ControlProcedureOperation CPO
)
SELECT DISTINCT TOP (100)
    CPR.manufacturingOrder        AS manufacturingOrder,
    CPR.manufacturingPhase        AS manufacturingPhase,
    CPR.batch                     AS batch,
    CPR.idControlProcedureResult  AS idControlProcedureResult,
    CAST(CPR.isManual AS bit)     AS isManual,
    CPR.workplace                 AS workplace,
    CPR.launchingDate             AS launchingDate,
    CPR.controlProcedure          AS controlProcedure,
    CPO.controlProcedureVersion   AS controlProcedureVersion,
    CPO.controlProcedureLevel     AS controlProcedureLevel,
    CPR.controlProcResultNote     AS controlProcedureNote,
    CPR.worker                    AS worker,
    CPO.controlOperation          AS controlOperation,
    CPO.controlOperationName      AS controlOperationNote,
    CAST(ISNULL(CPrvs.doesNotApply,0) AS bit) AS doesNotApply,
    CPrvs.resultNumber            AS resultNumber,
    CPrvs.resultAttribute         AS resultAttribute,
    CPrvs.resultValue             AS resultValue,
    CPrvs.resultPresetAttributeValue AS resultPresetAttributeValue,
    CPrvs.ctrlProcOpResultValNote AS controlOperationResultValueNote,
    CPrvs.controlOperationType    AS controlOperationType
FROM ControlProcedureResult CPR
LEFT JOIN CPO_Latest CPO
    ON  CPO.company          = CPR.company
    AND CPO.controlProcedure = CPR.controlProcedure
    AND CPO.rn               = 1
LEFT JOIN CProcResultWithValuesStatus CPrvs
    ON  CPrvs.company                  = CPR.company
    AND CPrvs.idControlProcedureResult = CPR.idControlProcedureResult
    AND CPrvs.controlOperation         = CPO.controlOperation
WHERE CPR.company = @company
  AND (@workplace IS NULL OR CPR.workplace = @workplace)
  AND (@manufacturingorder IS NULL OR CPR.manufacturingOrder = @manufacturingorder)
  AND (@startdate IS NULL OR CPR.launchingDate >= @startdate)
  AND (@enddate   IS NULL OR CPR.launchingDate <  @enddate)
  AND CPR.completionStatus = 0
ORDER BY CPR.idControlProcedureResult, CPO.controlOperation, CPrvs.resultNumber;
";

    public async Task<List<ACPayloadRow>> ObtenerRowsAsync(
        IDictionary<string, object> parametros,
        string connectionString)
    {
        using var conn = new SqlConnection(connectionString);
        var dynParams = new DynamicParameters();

        foreach (var kv in parametros)
        {
            dynParams.Add("@" + kv.Key, kv.Value);
        }

        var result = await conn.QueryAsync<ACPayloadRow>(
            SqlACPayloadRows,
            dynParams
        );

        return result.ToList();
    }
}
