// Services/ACPayloadService.cs
using Microsoft.Data.SqlClient;  
using Dapper;
using DocumentFormat.OpenXml.Spreadsheet;
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
SELECT DISTINCT 
    CPR.manufacturingOrder        AS manufacturingOrder,
    CPR.manufacturingPhase        AS manufacturingPhase,
    CPR.batch                     AS batch,
    CPR.idControlProcedureResult  AS idControlProcedureResult,
    CAST(CPR.isManual AS bit)     AS isManual,
    CPR.workplace                 AS workplace,
    CPR.launchingDate             AS launchingDate,
    CPR.controlProcedure          AS controlProcedure,
    CPR.controlProcedureVersion   AS controlProcedureVersion,
    CPR.controlProcedureLevel     AS controlProcedureLevel,
	CP.controlProcedureLevelName as controlProcedureLevelName,
    CPR.controlProcResultNote     AS controlProcedureNote,
    CPR.worker                    AS worker,
    CPO.controlOperation          AS controlOperation,
    CPO.controlOperationName      AS controlOperationNote,	
	CPO.sampleSize      AS sampleSize,
	CPO.position   as position   ,
    CAST(ISNULL(CPrvs.doesNotApply,0) AS bit) AS doesNotApply,
    CPrvs.resultNumber            AS resultNumber,
    CPrvs.resultAttribute         AS resultAttribute,
    CPrvs.resultValue             AS resultValue,
    CPrvs.resultPresetAttributeValue AS resultPresetAttributeValue,
    CPrvs.ctrlProcOpResultValNote AS controlOperationResultValueNote,
    CPO.controlOperationType    AS controlOperationType
FROM ControlProcedureResult CPR
LEFT JOIN ControlProcedureOperation CPO
    ON  CPO.company          = CPR.company
    AND CPO.controlProcedure = CPR.controlProcedure
	AND CPO.controlProcedureVersion = CPR.controlProcedureVersion
	AND CPO.controlProcedureLevel = CPR.controlProcedureLevel
left join ControlProcedure CP 
	on CP.controlProcedure = CPR.controlProcedure 
	and CP.controlProcedureVersion = CPR.controlProcedureVersion
    and CP.controlProcedureLevel = CPR.controlProcedureLevel    
	and CP.company = CPO.company
LEFT JOIN CProcResultWithValuesStatus CPrvs
    ON  CPrvs.company                  = CPR.company
    AND CPrvs.idControlProcedureResult = CPR.idControlProcedureResult
    AND CPrvs.controlOperation         = CPO.controlOperation
WHERE CPR.company = @company
  AND CPR.workplace = @workplace
  AND CPR.launchingDate >= @startdate
  AND CPR.launchingDate <  @enddate
  AND CPR.isManual = 0
  and CPR.executionDate is null
ORDER BY CPR.launchingDate, CPR.idControlProcedureResult, CPR.controlProcedureVersion ,
CPR.controlProcedureLevel, CPO.position
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

        var baseList = result.ToList();
        var finalList = new List<ACPayloadRow>();

        foreach (var row in baseList)
        {
            var sampleSize = row.SampleSize <= 0 ? 1 : row.SampleSize;

            // Si algún día viniera resultNumber desde la BD, lo respetamos
            if (!string.IsNullOrEmpty(row.resultNumber))
            {
                finalList.Add(row);
                continue;
            }

            // Siempre: 0..sampleSize-1, aunque sampleSize sea 1
            for (int i = 0; i < sampleSize; i++)
            {
                finalList.Add(CloneRowWithResultNumber(row, i.ToString()));
            }
        }


        return finalList;
    }

    // helper local (puede ser private static en la misma clase)
    private static int SafeParseInt(string? s)
    {
        if (int.TryParse(s, out var n)) return n;
        return 0;
    }

    private static ACPayloadRow CloneRowWithResultNumber(ACPayloadRow src, string resultNumber)
    {
        return new ACPayloadRow
        {
            manufacturingOrder = src.manufacturingOrder,
            manufacturingPhase = src.manufacturingPhase,
            batch = src.batch,
            idControlProcedureResult = src.idControlProcedureResult,
            isManual = src.isManual,
            workplace = src.workplace,
            launchingDate = src.launchingDate,
            controlProcedure = src.controlProcedure,
            controlProcedureVersion = src.controlProcedureVersion,
            controlProcedureLevel = src.controlProcedureLevel,
            controlProcedureNote = src.controlProcedureNote,
            worker = src.worker,
            controlOperation = src.controlOperation,
            SampleSize = src.SampleSize,
            controlOperationNote = src.controlOperationNote,
            doesNotApply = src.doesNotApply,

            // aquí aplicamos la regla
            resultNumber = resultNumber,

            // estos por ahora los dejamos igual (vendrán null
            // y luego los llenarás desde Excel / UI)
            resultAttribute = src.resultAttribute,
            resultValue = src.resultValue,
            resultPresetAttributeValue = src.resultPresetAttributeValue,
            controlOperationResultValueNote = src.controlOperationResultValueNote,
            controlProcedureLevelName = src.controlProcedureLevelName,
            controlOperationType = src.controlOperationType
        };
    }

}
