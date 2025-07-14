using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Text.RegularExpressions;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Services
{
    public class TrazabilityClass
    {
        public static DataTable GetBackwardTraceability(string company, long batch, string connectionString)
        {
            // Configura tu cadena de conexión aquí
            //string connectionString = "Server=PCMX01\\SQLSERVER2017;Database=captor;User Id=sa;Password=Sisteplant+2017;";

            // Consulta SQL recursiva
            string sqlQuery = @"
                WITH RecursivoCTE AS (
 SELECT 
	RC.company,
	RC.consumedReference as manufacturingReference,
    MR.manufacturingReferenceName as manufacturingReferenceName,
	RC.consumedBatch as batch,
	RC.batch as Batchpadre, 
    RC.batchIdentifier,
    RC.batchIdentifier as batchname,
    RC.startDate,RC.endDate,
    1 AS Nivel -- Nivel 0 para los padres
FROM TraceabilityNodeRelations RC	
inner join manufacturingreference MR on MR.manufacturingReference = RC.manufacturingReference and MR.company = RC.company
WHERE RC.company = @company and RC.batch = @batch 
 UNION ALL
  -- Recursividad: encuentra los hijos
    SELECT 
        t.company,
        t.consumedReference as manufacturingReference,
        MR2.manufacturingReferenceName as manufacturingReferenceName,
        t.consumedBatch as batch,
        t.batch as Batchpadre, 
        t.batchIdentifier,
        bth.batchIdentifier as batchname,
        t.startDate,t.endDate,
        cte.Nivel + 1 -- Incrementa el nivel para los hijos
    FROM TraceabilityNodeRelations t
    inner join manufacturingreference MR2 on MR2.manufacturingReference = t.manufacturingReference and MR2.company = t.company
    inner join Batch bth on bth.batch = t.consumedBatch and bth.company = t.company
    INNER JOIN RecursivoCTE cte
        ON t.batch = cte.batch and t.company = @company
),
RowNumCTE AS (
    SELECT *,
           ROW_NUMBER() OVER (PARTITION BY Nivel ORDER BY batch) AS rn
    FROM RecursivoCTE
)
-- Selección final
SELECT distinct
	RC3.manufacturingReference as Padre,
	null as Hijo,
	RC3.company,
	RC3.manufacturingReference ,
    MR2.manufacturingReferenceName as manufacturingReferenceName,
	RC3.batch,
	0 as Batchpadre,
    RC3.batchIdentifier,
    RC3.batchIdentifier as batchname,
    RC3.startDate,RC3.endDate,
	0, 
    --CPR.controlProcedure, CPR.launchingDate, 
	--CPrvs.resultValue, CPrvs.resultAttribute, CPrvs.maxTolerance, CPrvs.minTolerance, CPrvs.clientMinValue, CPrvs.clientMaxValue, 
	--CPrvs.workplace, CPrvs.worker, CPrvs.controlOperationName, 
	--CPrvs.nominalValue, CPrvs.controlProcResultNote, CPrvs.resultNumber, CPrrc.shift, CPrrc.manufacturingOrder, 
	--CPrrc.manufacturingReference,
    0 AS Nivel -- Nivel 0 para los padres
FROM TraceabilityNodeRelations RC3	
inner join manufacturingreference MR2 on MR2.manufacturingReference = RC3.manufacturingReference and MR2.company = RC3.company
--left join ControlProcedureResult CPR on CPR.company = RC3.company and CPR.batch = RC3.batch
--left join CProcResultWithValuesStatus CPrvs on CPrvs.company = CPR.company and CPrvs.idControlProcedureResult = CPR.idControlProcedureResult
--left join CPResultWithRefAndContext CPrrc on CPrvs.company = CPrrc.company and CPrvs.idControlProcedureResult = CPrrc.idControlProcedureResult
WHERE RC3.company = @company and RC3.batch = @batch 
union all
SELECT
    CASE 
        WHEN Nivel = 0 AND rn = 1 
        THEN CTE.manufacturingReference 
        ELSE NULL 
    END AS Padre,
    CASE WHEN Nivel > 0 THEN CTE.manufacturingReference ELSE NULL END AS Hijo,
    CTE.company,    
    CTE.manufacturingReference,
    MR3.manufacturingReferenceName as manufacturingReferenceName,
	CTE.batch,
    CTE.Batchpadre,
    CTE.batchIdentifier,
    bth3.batchIdentifier as batchname,
    CTE.startDate,CTE.endDate,
    rn, 
    --CPR.controlProcedure, CPR.launchingDate, 
	--CPrvs.resultValue, CPrvs.resultAttribute, CPrvs.maxTolerance, CPrvs.minTolerance, CPrvs.clientMinValue, CPrvs.clientMaxValue, 
	--CPrvs.workplace, CPrvs.worker, CPrvs.controlOperationName, 
	--CPrvs.nominalValue, CPrvs.controlProcResultNote, CPrvs.resultNumber, CPrrc.shift, CPrrc.manufacturingOrder, 
	--CPrrc.manufacturingReference,
	Nivel
FROM RowNumCTE CTE
inner join manufacturingreference MR3 on MR3.manufacturingReference = CTE.manufacturingReference and MR3.company = CTE.company
inner join Batch bth3 on bth3.batch = CTE.batch and bth3.company = CTE.company
--left join ControlProcedureResult CPR on CPR.company = CTE.company and CPR.batch = CTE.batch
--left join CProcResultWithValuesStatus CPrvs on CPrvs.company = CPR.company and CPrvs.idControlProcedureResult = CPR.idControlProcedureResult
--left join CPResultWithRefAndContext CPrrc on CPrvs.company = CPrrc.company and CPrvs.idControlProcedureResult = CPrrc.idControlProcedureResult
-- Condición corregida para filtrar registros donde Padre y Hijo sean NULL
WHERE NOT ( CTE.batch is null );
            ";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                {
                    // Añadir parámetros
                    command.Parameters.AddWithValue("@company", company);
                    command.Parameters.AddWithValue("@batch", batch);

                    // Ejecutar la consulta y llenar el DataTable
                    DataTable resultTable = new DataTable();
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    adapter.Fill(resultTable);
                    return resultTable;
                }
            }
        }

        public static BatchInfo GetBatchInfoByText(string searchText, string connectionString, string company)
        {
            var info = new BatchInfo();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
            SELECT B.batch, BC.startDate, BC.endDate
            FROM Batch B
            INNER JOIN BatchCreation BC ON B.batch = BC.batch AND B.company = BC.company
            WHERE B.company = @company AND B.batchIdentifier LIKE @searchText";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@searchText", "%" + searchText + "%");
                    command.Parameters.AddWithValue("@company", company);
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            info.BatchId = reader.GetInt32(0);                    // batch
                            info.StartDate = reader.IsDBNull(1) ? null : reader.GetDateTime(1); // startDate
                            info.EndDate = reader.IsDBNull(2) ? null : reader.GetDateTime(2);   // endDate
                        }
                    }
                }
            }

            return info;
        }


        public static DataTable GetChecklistByDateRangeAndReference(DataTable trazabilidad, string company, string connectionString)
        {
            var checklistTable = new DataTable();
            checklistTable.Columns.Add("batch", typeof(long));
            checklistTable.Columns.Add("manufacturingReference", typeof(string));
            checklistTable.Columns.Add("controlOperationName", typeof(string));
            checklistTable.Columns.Add("resultValue", typeof(double));
            checklistTable.Columns.Add("minTolerance", typeof(double));
            checklistTable.Columns.Add("maxTolerance", typeof(double));

            //string connectionString = "Server=PCMX01\\SQLSERVER2017;Database=captor;User Id=sa;Password=Sisteplant+2017;";

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (DataRow row in trazabilidad.Rows)
                {
                    if (row["batch"] == DBNull.Value || row["manufacturingReference"] == DBNull.Value)
                        continue;

                    long batch = Convert.ToInt64(row["batch"]);
                    string reference = row["manufacturingReference"].ToString();

                    // Obtener las fechas del batch
                    var cmdFecha = new SqlCommand(@"SELECT startDate, endDate FROM TraceabilityNodeRelations WHERE company = @company AND batch = @batch", connection);
                    cmdFecha.Parameters.AddWithValue("@company", company);
                    cmdFecha.Parameters.AddWithValue("@batch", batch);
                    SqlDataReader readerFecha = cmdFecha.ExecuteReader();

                    DateTime? startDate = null;
                    DateTime? endDate = null;

                    if (readerFecha.Read())
                    {
                        startDate = readerFecha["startDate"] as DateTime?;
                        endDate = readerFecha["endDate"] as DateTime?;
                    }
                    readerFecha.Close();

                    if (startDate == null || endDate == null)
                        continue;

                    // Obtener checklist durante ese periodo para esa referencia
                    var cmdChecklist = new SqlCommand(@"
                        SELECT CPR.batch, CPrrc.manufacturingReference, V.controlOperationName, V.resultValue, V.minTolerance, V.maxTolerance, V.workplace
                        FROM ControlProcedureResult CPR
                        INNER JOIN CProcResultWithValuesStatus V ON V.idControlProcedureResult = CPR.idControlProcedureResult AND V.company = CPR.company
                        left join CPResultWithRefAndContext CPrrc on V.company = CPrrc.company and V.idControlProcedureResult = CPrrc.idControlProcedureResult
                        WHERE CPR.company = @company AND CPR.manufacturingOrder = @reference
                        AND CPR.launchingDate BETWEEN @start AND @end", connection);

                    cmdChecklist.Parameters.AddWithValue("@company", company);
                    cmdChecklist.Parameters.AddWithValue("@reference", reference);
                    cmdChecklist.Parameters.AddWithValue("@start", startDate);
                    cmdChecklist.Parameters.AddWithValue("@end", endDate);

                    SqlDataAdapter adapter = new SqlDataAdapter(cmdChecklist);
                    adapter.Fill(checklistTable);
                }
            }

            return checklistTable;
        }

        public static DataTable GetChecklistByTraceability(DataTable trazabilidad, string company, string connectionString)
        {
            var checklist = new DataTable();
            //string connectionString = "Server=PCMX01\\SQLSERVER2017;Database=captor;User Id=sa;Password=Sisteplant+2017;";

            // Extraer los lotes y fechas relevantes
            var condiciones = new List<string>();
            var parameters = new List<SqlParameter>();
            int index = 0;

            foreach (DataRow row in trazabilidad.Rows)
            {
                var batch = row["batch"].ToString();
                var reference = row["manufacturingReference"].ToString();
                var startDate = row.Table.Columns.Contains("startDate") ? row["startDate"] : DBNull.Value;
                var endDate = row.Table.Columns.Contains("endDate") ? row["endDate"] : DBNull.Value;

                if (!string.IsNullOrEmpty(batch))
                {
                    condiciones.Add($"(CPR.company = @company AND CPR.batch = @batch{index})");
                    parameters.Add(new SqlParameter($"@batch{index}", batch));
                }

                if (startDate != DBNull.Value && endDate != DBNull.Value)
                {
                    condiciones.Add($"(CPR.company = @company AND CPR.executionDate is not NULL AND CPR.manufacturingOrder = @ref{index} AND CPR.launchingDate BETWEEN @start{index} AND @end{index})");
                    parameters.Add(new SqlParameter($"@ref{index}", reference));
                    parameters.Add(new SqlParameter($"@start{index}", Convert.ToDateTime(startDate)));
                    parameters.Add(new SqlParameter($"@end{index}", Convert.ToDateTime(endDate)));
                }

                index++;
            }

            if (condiciones.Count == 0) return checklist;

            string query = $@"
                SELECT CPR.batch, CPrrc.manufacturingReference, CPR.controlProcedure, CPR.launchingDate,CPR.executionDate,
                       CPR.idControlProcedureResult, CPR.company, VS.workplace,
                       VS.resultValue, VS.resultAttribute, VS.maxTolerance, VS.minTolerance, VS.controlOperationName
                FROM ControlProcedureResult CPR
                LEFT JOIN CProcResultWithValuesStatus VS ON CPR.company = VS.company AND CPR.idControlProcedureResult = VS.idControlProcedureResult
                left join CPResultWithRefAndContext CPrrc on VS.company = CPrrc.company and VS.idControlProcedureResult = CPrrc.idControlProcedureResult
                WHERE CPR.company = @company AND ({string.Join(" OR ", condiciones)})";

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                cmd.Parameters.AddWithValue("@company", company);
                foreach (var p in parameters) cmd.Parameters.Add(p);

                // 🔎 Mostrar query con parámetros reemplazados para debug
                string simulatedQuery = query;
                foreach (SqlParameter param in cmd.Parameters)
                {
                    string valueFormatted;

                    if (param.Value == DBNull.Value || param.Value == null)
                        valueFormatted = "NULL";
                    else if (param.Value is DateTime dt)
                        valueFormatted = $"'{dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'";
                    else if (param.Value is string s)
                        valueFormatted = $"'{s.Replace("'", "''")}'";
                    else if (param.Value is bool b)
                        valueFormatted = b ? "1" : "0";
                    else
                        valueFormatted = param.Value.ToString();

                    // 🛡️ Reemplaza solo el parámetro exacto (por ejemplo, @batch0 y no @batch01)
                    simulatedQuery = Regex.Replace(simulatedQuery, $@"(?<!\w){Regex.Escape(param.ParameterName)}(?!\w)", valueFormatted);

                }

                System.Diagnostics.Debug.WriteLine("🔍 Query SQL simulado para SSMS:");
                System.Diagnostics.Debug.WriteLine(simulatedQuery);

                adapter.Fill(checklist);
            }

            return checklist;
        }
    }
}
