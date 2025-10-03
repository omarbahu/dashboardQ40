using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace dashboardQ40.Services
{
    public class XRchartsService
    {
        // =========================================================
        // 1) SELECT base (el mismo que probaste, con alias limpios)
        // =========================================================
        public static DataTable GetXRBaseRows(
            string company,
            DateTime from,
            DateTime to,
            string connectionString,
            string? workplace = null,
            string? reference = null,
            string? controlOperation = null)
        {
            const string sql = @"
SELECT
  C.companyName,
  CPrvs.workplace,
  W.workplaceName,
  MR.manufacturingReferenceName,
  CPrvs.controlOperation,
  CPrvs.controlOperationName,
  CPrrc.idControlProcedureResult,
  CPrvs.manufacturingReference,
  CONCAT(CPrrc.idControlProcedureResult, '::', CPrvs.controlOperation) AS subgroupId,
  CPrrc.executionDate,
  CAST(CPrvs.resultValue AS decimal(18,6)) AS resultValue,
  CAST(COALESCE(CPrvs.clientMinValue, CPrvs.minTolerance) AS decimal(18,6)) AS LSL,
  CAST(COALESCE(CPrvs.clientMaxValue, CPrvs.maxTolerance) AS decimal(18,6)) AS USL,
  CAST(CPrvs.nominalValue AS decimal(18,6)) AS Target,
  CASE
    WHEN COALESCE(CPrvs.clientMinValue, CPrvs.minTolerance) IS NULL
         AND COALESCE(CPrvs.clientMaxValue, CPrvs.maxTolerance) IS NOT NULL THEN 'USL-only'
    WHEN COALESCE(CPrvs.clientMaxValue, CPrvs.maxTolerance) IS NULL
         AND COALESCE(CPrvs.clientMinValue, CPrvs.minTolerance) IS NOT NULL THEN 'LSL-only'
    WHEN COALESCE(CPrvs.clientMinValue, CPrvs.minTolerance) IS NOT NULL
         AND COALESCE(CPrvs.clientMaxValue, CPrvs.maxTolerance) IS NOT NULL THEN 'Two-sided'
    ELSE 'No-spec'
  END AS specType,
  COUNT(*) OVER (PARTITION BY CPrrc.idControlProcedureResult, CPrvs.controlOperation) AS subgroupN
FROM CProcResultWithValuesStatus AS CPrvs
JOIN CPResultWithRefAndContext AS CPrrc
  ON CPrvs.company = CPrrc.company
 AND CPrvs.idControlProcedureResult = CPrrc.idControlProcedureResult
JOIN Company AS C
  ON CPrrc.company = C.company
JOIN Workplace AS W
  ON CPrvs.workplace = W.workplace
 AND W.company = CPrvs.company
JOIN ManufacturingReference AS MR
  ON MR.company = CPrvs.company
 AND MR.manufacturingReference = CPrvs.manufacturingReference
WHERE
  CPrvs.company = @company
  AND CPrvs.controlOperationType = 1
  AND CPrrc.executionDate BETWEEN @from AND @to
  AND CPrvs.resultValue IS NOT NULL
  AND (@workplace IS NULL OR CPrvs.workplace = @workplace)
  --AND (@reference IS NULL OR CPrvs.manufacturingReference = @reference)
  AND (@controlOperation IS NULL OR CPrvs.controlOperation = @controlOperation)
ORDER BY
  CPrvs.controlOperation,
  CPrrc.executionDate,
  resultValue;";

            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@company", company);
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);
            cmd.Parameters.AddWithValue("@workplace", (object?)workplace ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@reference", (object?)reference ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@controlOperation", (object?)controlOperation ?? DBNull.Value);

            // Log para copiar/pegar en SSMS
            System.Diagnostics.Debug.WriteLine(BuildSimulatedQuery(sql, cmd));

            var dt = new DataTable();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(dt);
            return dt;
        }

        // ==========================================
        // 2) Variables disponibles (para dropdowns)
        // ==========================================
        public static DataTable GetVariables(
            string company,
            DateTime from,
            DateTime to,
            string connectionString,
            string? workplace = null,
            string? reference = null)
        {
            const string sql = @"
SELECT DISTINCT
  CPrvs.controlOperation,
  CPrvs.controlOperationName
FROM CProcResultWithValuesStatus AS CPrvs
JOIN CPResultWithRefAndContext AS CPrrc
  ON CPrvs.company = CPrrc.company
 AND CPrvs.idControlProcedureResult = CPrrc.idControlProcedureResult
WHERE CPrvs.company = @company
  AND CPrvs.controlOperationType = 1
  AND CPrrc.executionDate BETWEEN @from AND @to
  AND CPrvs.resultValue IS NOT NULL
  AND (@workplace IS NULL OR CPrvs.workplace = @workplace)
  AND (@reference IS NULL OR CPrvs.manufacturingReference = @reference)
ORDER BY CPrvs.controlOperation;";

            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("@company", company);
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);
            cmd.Parameters.AddWithValue("@workplace", (object?)workplace ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@reference", (object?)reference ?? DBNull.Value);

            System.Diagnostics.Debug.WriteLine(BuildSimulatedQuery(sql, cmd));

            var dt = new DataTable();
            using var da = new SqlDataAdapter(cmd);
            da.Fill(dt);
            return dt;
        }

        // ==================================================
        // 3) Subgroup stats (Xbar-S) a partir del SELECT base
        //     -> devuelve: subgroupId, N, Xbar, S
        // ==================================================
        public static DataTable BuildSubgroupStats(DataTable baseRows)
        {
            var result = new DataTable();
            result.Columns.Add("subgroupId", typeof(string));
            result.Columns.Add("N", typeof(int));
            result.Columns.Add("Xbar", typeof(double));
            result.Columns.Add("S", typeof(double)); // puede ir DBNull

            var groups = baseRows.AsEnumerable()
                .GroupBy(r => r.Field<string>("subgroupId"));

            foreach (var g in groups)
            {
                var vals = g.Select(r => Convert.ToDouble(r["resultValue"])).ToList();
                var n = vals.Count;
                var mean = vals.Average();
                var s = SampleStdDev(vals);

                var row = result.NewRow();
                row["subgroupId"] = g.Key;
                row["N"] = n;
                row["Xbar"] = mean;
                row["S"] = (object?)s ?? DBNull.Value;
                result.Rows.Add(row);
            }

            return result;
        }

        // ==================================================
        // 4) Capability por variable (Cp/Cpk y Pp/Ppk)
        //     -> agrupa por controlOperation
        // ==================================================
        public static DataTable BuildCapability(DataTable baseRows)
        {
            var table = new DataTable();
            table.Columns.Add("controlOperation", typeof(string));
            table.Columns.Add("controlOperationName", typeof(string));
            table.Columns.Add("nPoints", typeof(int));
            table.Columns.Add("nSubgroups", typeof(int));
            table.Columns.Add("meanAll", typeof(double));
            table.Columns.Add("sigmaOverall", typeof(double));  // Pooled overall (s muestral)
            table.Columns.Add("sigmaWithin", typeof(double));   // Pooled within (de subgrupos) o IMR
            table.Columns.Add("LSL", typeof(double));
            table.Columns.Add("USL", typeof(double));
            table.Columns.Add("specType", typeof(string));
            table.Columns.Add("Cp", typeof(double));
            table.Columns.Add("Cpk", typeof(double));
            table.Columns.Add("Pp", typeof(double));
            table.Columns.Add("Ppk", typeof(double));

            var groups = baseRows.AsEnumerable()
                .GroupBy(r => new
                {
                    Op = r.Field<string>("controlOperation"),
                    OpName = r.Field<string>("controlOperationName"),
                    SpecType = r.Field<string>("specType")
                });

            foreach (var g in groups)
            {
                var vals = g.Select(r => Convert.ToDouble(r["resultValue"])).ToList();
                if (vals.Count == 0) continue;

                var mean = vals.Average();
                var sOverall = SampleStdDev(vals); // muestral

                // LSL/USL/Target: tomamos el primer no nulo (asumiendo constancia)
                double? LSL = g.Select(r => r["LSL"])
                               .Where(v => v != DBNull.Value)
                               .Select(v => (double?)Convert.ToDouble(v))
                               .FirstOrDefault();

                double? USL = g.Select(r => r["USL"])
                               .Where(v => v != DBNull.Value)
                               .Select(v => (double?)Convert.ToDouble(v))
                               .FirstOrDefault();


                // Stats por subgrupo
                var subs = g.GroupBy(r => r.Field<string>("subgroupId"))
                            .Select(sg =>
                            {
                                var v = sg.Select(r => Convert.ToDouble(r["resultValue"])).ToList();
                                return new
                                {
                                    N = v.Count,
                                    S = SampleStdDev(v)
                                };
                            })
                            .ToList();

                var sigmaWithin = PooledSigmaWithin(subs) ?? ImrSigmaWithin(
                    g.OrderBy(r => r.Field<DateTime>("executionDate"))
                     .ThenBy(r => r.Field<string>("subgroupId"))
                     .Select(r => Convert.ToDouble(r["resultValue"]))
                     .ToList()
                );

                var (cp, cpk) = Capability(LSL, USL, mean, sigmaWithin);
                var (pp, ppk) = Capability(LSL, USL, mean, sOverall);

                var row = table.NewRow();
                row["controlOperation"] = g.Key.Op;
                row["controlOperationName"] = g.Key.OpName;
                row["nPoints"] = vals.Count;
                row["nSubgroups"] = subs.Count;
                row["meanAll"] = mean;
                row["sigmaOverall"] = (object?)sOverall ?? DBNull.Value;
                row["sigmaWithin"] = (object?)sigmaWithin ?? DBNull.Value;
                row["LSL"] = (object?)LSL ?? DBNull.Value;
                row["USL"] = (object?)USL ?? DBNull.Value;
                row["specType"] = g.Key.SpecType;
                row["Cp"] = (object?)cp ?? DBNull.Value;
                row["Cpk"] = (object?)cpk ?? DBNull.Value;
                row["Pp"] = (object?)pp ?? DBNull.Value;
                row["Ppk"] = (object?)ppk ?? DBNull.Value;

                table.Rows.Add(row);
            }

            return table;
        }

        // =====================
        // ---- Helper math ----
        // =====================
        private static double? SampleStdDev(IList<double> values)
        {
            if (values == null || values.Count < 2) return null;
            var mean = values.Average();
            var sumSq = values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumSq / (values.Count - 1));
        }

        private static double? PooledSigmaWithin(IEnumerable<dynamic> subs)
        {
            // subs: { int N; double? S; }
            double num = 0;
            int den = 0;

            foreach (var s in subs)
            {
                if (s.S == null || s.N < 2) continue;
                num += (s.N - 1) * Math.Pow((double)s.S, 2);
                den += (s.N - 1);
            }

            if (den <= 0) return null;
            return Math.Sqrt(num / den);
        }

        private static double? ImrSigmaWithin(IList<double> orderedValues)
        {
            // Método alternativo (I-MR), d2 para n=2
            if (orderedValues == null || orderedValues.Count < 2) return null;
            const double d2 = 1.128;

            var mrs = new List<double>();
            for (int i = 1; i < orderedValues.Count; i++)
                mrs.Add(Math.Abs(orderedValues[i] - orderedValues[i - 1]));

            if (mrs.Count == 0) return null;
            return mrs.Average() / d2;
        }

        private static (double? cp, double? cpk) Capability(double? lsl, double? usl, double mean, double? sigma)
        {
            if (!sigma.HasValue || sigma.Value <= 0) return (null, null);

            double? cp = null, cpk = null;
            if (lsl.HasValue && usl.HasValue)
            {
                cp = (usl.Value - lsl.Value) / (6.0 * sigma.Value);
                var cpu = (usl.Value - mean) / (3.0 * sigma.Value);
                var cpl = (mean - lsl.Value) / (3.0 * sigma.Value);
                cpk = Math.Min(cpu, cpl);
            }
            else if (usl.HasValue)
            {
                // One-sided informativo
                cp = (usl.Value - mean) / (3.0 * sigma.Value) * 2.0;
                cpk = (usl.Value - mean) / (3.0 * sigma.Value);
            }
            else if (lsl.HasValue)
            {
                cp = (mean - lsl.Value) / (3.0 * sigma.Value) * 2.0;
                cpk = (mean - lsl.Value) / (3.0 * sigma.Value);
            }

            return (cp, cpk);
        }

        // ===================================
        // ---- Helper: log query “simulada”
        // ===================================
        private static string BuildSimulatedQuery(string sql, SqlCommand cmd)
        {
            var simulated = sql;
            foreach (SqlParameter p in cmd.Parameters)
            {
                string val;
                if (p.Value == null || p.Value == DBNull.Value) val = "NULL";
                else if (p.Value is DateTime dt) val = $"'{dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'";
                else if (p.Value is string s) val = $"'{s.Replace("'", "''")}'";
                else if (p.Value is bool b) val = b ? "1" : "0";
                else val = Convert.ToString(p.Value, CultureInfo.InvariantCulture) ?? "NULL";

                simulated = Regex.Replace(simulated,
                    $@"(?<!\w){Regex.Escape(p.ParameterName)}(?!\w)", val);
            }

            return "/* SQL simulado para SSMS */\n" + simulated;
        }
    }
}
