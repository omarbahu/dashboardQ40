using System.Data;
using Microsoft.Data.SqlClient;
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

                    string queryLog = sqlQuery
    .Replace("@company", $"'{company}'")
    .Replace("@batch", $"'{batch}'");

                    Console.WriteLine("SQL ejecutado:\n" + queryLog);

                    // Ejecutar la consulta y llenar el DataTable
                    DataTable resultTable = new DataTable();
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    adapter.Fill(resultTable);

                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(resultTable, Newtonsoft.Json.Formatting.Indented);
                    Console.WriteLine("Resultado JSON:\n" + json);

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
            SELECT B.batch, BC.startDate, BC.endDate, BC.workplace, BC.manufacturingReference, BC.quantity, BC.batchCreation, B.initialQuantity 
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
                            info.BatchCreation = (int)reader.GetInt32(6);
                            info.workplace = reader.GetString(3);
                            info.manufacturingReference = reader.GetString(4);
                            info.quantity = (long)reader.GetDecimal(5);
                            info.initialQuantity = (long)reader.GetDecimal(7);
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


        public static DataTable GetTraceabilityAudit(string company, long batch, string connectionString)
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
    RC.startDate,RC.endDate,MR.isRawMaterial, MR.manufacturingFamily,MF.manufacturingFamilyName,w.workplace,w.workplacename,--RC.bcquantity, RC.consumedquantity,
    1 AS Nivel -- Nivel 0 para los padres
FROM TraceabilityNodeRelations RC	
inner join manufacturingreference MR on MR.manufacturingReference = RC.manufacturingReference and MR.company = RC.company
inner join ManufacturingFamily MF on MR.manufacturingFamily = MF.manufacturingFamily and MR.company = MF.company
inner join workplace w on RC.workplace = w.workplace and w.company = RC.company
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
        t.startDate,t.endDate,MR2.isRawMaterial,MR2.manufacturingFamily,MF2.manufacturingFamilyName,w2.workplace,w2.workplacename,--t.bcquantity, t.consumedquantity,
        cte.Nivel + 1 -- Incrementa el nivel para los hijos
    FROM TraceabilityNodeRelations t
    inner join manufacturingreference MR2 on MR2.manufacturingReference = t.manufacturingReference and MR2.company = t.company
    inner join ManufacturingFamily MF2 on MR2.manufacturingFamily = MF2.manufacturingFamily and MR2.company = MF2.company
    inner join workplace w2 on t.workplace = w2.workplace and w2.company = t.company
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
    RC3.startDate,RC3.endDate,MR2.isRawMaterial,MR2.manufacturingFamily,MF2.manufacturingFamilyName,w2.workplace,w2.workplacename,--RC3.bcquantity, RC3.consumedquantity,
	0, 
    0 AS Nivel -- Nivel 0 para los padres
FROM TraceabilityNodeRelations RC3	
inner join manufacturingreference MR2 on MR2.manufacturingReference = RC3.manufacturingReference and MR2.company = RC3.company
inner join ManufacturingFamily MF2 on MR2.manufacturingFamily = MF2.manufacturingFamily and MR2.company = MF2.company
inner join workplace w2 on RC3.workplace = w2.workplace and w2.company = RC3.company
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
    CTE.startDate,CTE.endDate,MR3.isRawMaterial,MR3.manufacturingFamily,MF3.manufacturingFamilyName,w3.workplace,w3.workplacename,--CTE.bcquantity, CTE.consumedquantity,
    rn, 
	Nivel
FROM RowNumCTE CTE
inner join manufacturingreference MR3 on MR3.manufacturingReference = CTE.manufacturingReference and MR3.company = CTE.company
inner join ManufacturingFamily MF3 on MR3.manufacturingFamily = MF3.manufacturingFamily and MR3.company = MF3.company
inner join Batch bth3 on bth3.batch = CTE.batch and bth3.company = CTE.company
inner join workplace w3 on CTE.workplace = w3.workplace and w3.company = CTE.company
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

                    string queryLog = sqlQuery
    .Replace("@company", $"'{company}'")
    .Replace("@batch", $"'{batch}'");

                    Console.WriteLine("SQL ejecutado:\n" + queryLog);

                    // Ejecutar la consulta y llenar el DataTable
                    DataTable resultTable = new DataTable();
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    adapter.Fill(resultTable);

                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(resultTable, Newtonsoft.Json.Formatting.Indented);
                    Console.WriteLine("Resultado JSON:\n" + json);

                    return resultTable;
                }
            }
        }


        public static DataTable GetBatchbyDates(string company, DateTime F1, DateTime F2, string connectionString)
        {
            const string sqlQuery = @"
        select distinct b.batchIdentifier, b.manufacturingReference, MR.manufacturingReferenceName, 
               W.workplace, w.workplaceName, B.startDate, B.endDate
        from TraceabilityNodeRelations B
        inner join ManufacturingReference MR 
            on B.company = MR.company and B.manufacturingReference = MR.manufacturingReference
        inner join Workplace W 
            on W.company = B.company and W.workplace = B.consumptionWorkplace
        where B.startDate between @fecha1 and @fecha2 
          and B.company = @company
        order by b.batchIdentifier;
    ";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sqlQuery, connection))
            {
                // SIEMPRE agregamos el parámetro, aunque company sea null o vacío
                command.Parameters.Add("@company", SqlDbType.VarChar, 10).Value =
                    (object?)company ?? DBNull.Value;

                command.Parameters.Add("@fecha1", SqlDbType.DateTime).Value = F1;
                command.Parameters.Add("@fecha2", SqlDbType.DateTime).Value = F2;

                // Log con formato ISO para que lo puedas pegar en SSMS
                string queryLog = sqlQuery
                    .Replace("@company", $"'{company}'")
                    .Replace("@fecha1", $"'{F1:yyyy-MM-dd HH:mm:ss}'")
                    .Replace("@fecha2", $"'{F2:yyyy-MM-dd HH:mm:ss}'");

                Console.WriteLine("SQL ejecutado:\n" + queryLog);

                var resultTable = new DataTable();
                var adapter = new SqlDataAdapter(command);
                adapter.Fill(resultTable);

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(resultTable, Newtonsoft.Json.Formatting.Indented);
                Console.WriteLine("Resultado JSON:\n" + json);

                return resultTable;
            }
        }


        private static long ToInt64(DataRow r, string col)
        {
            var v = r[col];
            return v == DBNull.Value ? 0L : Convert.ToInt64(v);  // acepta int, long, string numérica
        }

        private static bool ToBool(DataRow r, string col)
        {
            var v = r[col];
            if (v == DBNull.Value) return false;
            if (v is bool b) return b;
            // algunos campos vienen como 0/1 o "0"/"1"
            return Convert.ToInt32(v) != 0;
        }

        private static DateTime ToDateTime(DataRow r, string col)
        {
            var v = r[col];
            return v == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(v);
        }

        public static List<TrazabilidadNode> ConvertirDataTableATrazabilidad(DataTable dt)
        {
            var nodos = dt.AsEnumerable().Select(row => new TrazabilidadNode
            {
                Padre = row["Padre"] == DBNull.Value ? null : row["Padre"]?.ToString(),
                Hijo = row["Hijo"] == DBNull.Value ? null : row["Hijo"]?.ToString(),
                Company = row["company"]?.ToString(),
                ManufacturingReference = row["manufacturingReference"]?.ToString(),
                ManufacturingReferenceName = row["manufacturingReferenceName"]?.ToString(),

                Batch = ToInt64(row, "batch"),
                BatchPadre = ToInt64(row, "Batchpadre"),
                BatchIdentifier = row["batchIdentifier"]?.ToString(),
                BatchName = row["batchname"]?.ToString(),
                StartDate = ToDateTime(row, "startDate"),
                EndDate = ToDateTime(row, "endDate"),
                IsRawMaterial = ToBool(row, "isRawMaterial"),

                ManufacturingFamily = row["ManufacturingFamily"]?.ToString(),
                manufacturingFamilyName = row["manufacturingFamilyName"]?.ToString(),
                workplacename = row["workplacename"]?.ToString(),
                workplace = row["workplace"]?.ToString()
            });

            // === DEDUPLICA ===
            // Opción A: una fila por ARISTA (Company, BatchPadre, Batch)
            var dedup = nodos
                .GroupBy(n => (n.Company, n.BatchPadre, n.Batch))
                .Select(g => g.OrderByDescending(n => n.StartDate).First())
                .ToList();

            // // Opción B: una fila por NODO (Company, Batch)
            // var dedup = nodos
            //     .GroupBy(n => (n.Company, n.Batch))
            //     .Select(g => g.OrderByDescending(n => n.StartDate).First())
            //     .ToList();

            return dedup;
        }




    }
}
