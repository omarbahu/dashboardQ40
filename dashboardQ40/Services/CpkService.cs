using System.Data;
using Microsoft.Data.SqlClient;
using dashboardQ40.Models;
using System.Globalization;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Services
{
    public class CpkService
    {
        static readonly Dictionary<int, double> D2 = new()
        {
            {2,1.128},{3,1.693},{4,2.059},{5,2.326},{6,2.534},
            {7,2.704},{8,2.847},{9,2.970},{10,3.078},
            {15,3.472},{20,3.735},{25,3.924}
        };

        /// <summary>
        /// Ejecuta el CTE (agg + sg), obtiene límites (LSL/USL/Target) y devuelve el resumen Cp/Cpk.
        /// </summary>
        public static List<CapabilityRow> GetResumenCpk(
     string company, DateTime fromUtc, DateTime toUtc, string connectionString)
        {
            var agg = QueryAggOnly(company, fromUtc, toUtc, connectionString);
            var specs = LoadSpecsFromPeriod(company, fromUtc, toUtc, connectionString);
            // No tenemos detalle de subgrupos -> pasa lista vacía
            return ComputeCapability(agg, new List<SgRow>(), specs)
                    .OrderBy(r => r.Cpk ?? double.PositiveInfinity)
                    .ToList();
        }


        // =========  DB  =========

        // CTE: agg + detalle de subgrupos
        private static List<AggRow> QueryAggOnly(
    string company, DateTime fromUtc, DateTime toUtc, string cs)
        {
            var agg = new List<AggRow>();

            string sql = @"
;WITH raw AS (
  SELECT
 -- Línea
    (UPPER(LTRIM(RTRIM(W.workplaceName))) COLLATE Latin1_General_CI_AI)      AS Process,
    -- SKU
    (UPPER(LTRIM(RTRIM(MR.manufacturingReferenceName))) COLLATE Latin1_General_CI_AI) AS Part,
    -- Variable
    (UPPER(LTRIM(RTRIM(CPrvs.controlOperationName))) COLLATE Latin1_General_CI_AI)    AS Test,

    TRY_CONVERT(float, CPrvs.resultValue) AS Value,
    CPrrc.executionDate                   AS Ts,
    CPrrc.idControlProcedureResult        AS GroupId
  FROM CProcResultWithValuesStatus CPrvs
  JOIN CPResultWithRefAndContext CPrrc
    ON CPrvs.company = CPrrc.company
   AND CPrvs.idControlProcedureResult = CPrrc.idControlProcedureResult
  JOIN Workplace  W  ON W.company = CPrvs.company AND W.workplace = CPrvs.workplace
  JOIN ManufacturingReference MR
    ON MR.company = CPrvs.company AND MR.manufacturingReference = CPrvs.manufacturingReference
  WHERE CPrvs.company = @company
    AND CPrvs.controlOperationType = 1
    AND CPrrc.executionDate >= @from
    AND CPrrc.executionDate <  @to
    AND TRY_CONVERT(float, CPrvs.resultValue) IS NOT NULL
),
sg AS (
  SELECT
      r.Process, r.Part, r.Test,
      r.GroupId                                   AS SubgroupId,
      COUNT(*)                                    AS n,
      AVG(r.Value)                                AS xbar,
      MAX(r.Value) - MIN(r.Value)                 AS R,
      SUM(r.Value)                                AS sum_x,
      MIN(r.Ts)                                   AS SubgroupTs
  FROM raw r
  GROUP BY r.Process, r.Part, r.Test, r.GroupId
),
agg AS (
  SELECT
      s.Process, s.Part, s.Test,
      COUNT(*)                                         AS Subgroups,
      CAST(SUM(s.sum_x) AS float) / NULLIF(SUM(s.n),0) AS Mean,
      AVG(s.R)                                         AS Rbar,
      SUM(s.n)                                         AS Nobs,
      MIN(s.n)                                         AS n_min,
      MAX(s.n)                                         AS n_max,
      MIN(s.SubgroupTs)                                AS FirstTs,
      MAX(s.SubgroupTs)                                AS LastTs
  FROM sg s
  GROUP BY s.Process, s.Part, s.Test
)
SELECT
    a.Part,
    a.Process,
    a.Test,
    a.Subgroups,
    a.Mean,
    a.Rbar,
    a.Nobs,
    a.n_min, a.n_max,
    a.FirstTs, a.LastTs
FROM agg a
ORDER BY a.Process, a.Part, a.Test;";

            using var conn = new SqlConnection(cs);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@company", company);
            cmd.Parameters.AddWithValue("@from", fromUtc);
            cmd.Parameters.AddWithValue("@to", toUtc);

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                agg.Add(new AggRow(
                    Part: rd.GetString(0),
                    Process: rd.GetString(1),
                    Test: rd.GetString(2),
                    Subgroups: rd.GetInt32(3),
                    Mean: rd.IsDBNull(4) ? double.NaN : rd.GetDouble(4),
                    Rbar: rd.IsDBNull(5) ? double.NaN : rd.GetDouble(5),
                    Nobs: rd.GetInt32(6),
                    n_min: rd.GetInt32(7),
                    n_max: rd.GetInt32(8),
                    FirstTs: rd.GetDateTime(9),
                    LastTs: rd.GetDateTime(10)
                ));
            }
            return agg;
        }


        // LSL/USL/Target tomados del mismo periodo (si no tienes un catálogo maestro)
        // Agrupa por Part/Test y usa MIN/MAX/AVG de tolerancias/nominal.
        private static Dictionary<(string Part, string Test), Spec> LoadSpecsFromPeriod(
            string company, DateTime fromUtc, DateTime toUtc, string cs)
        {
            var dict = new Dictionary<(string Part, string Test), Spec>();

            string sql = @"
SELECT
  MR.manufacturingReferenceName AS Part,
  CPrvs.controlOperationName    AS Test,
  MIN(TRY_CONVERT(float, CPrvs.minTolerance)) AS LSL,
  MAX(TRY_CONVERT(float, CPrvs.maxTolerance)) AS USL,
  AVG(TRY_CONVERT(float, CPrvs.nominalValue))  AS Target
FROM CProcResultWithValuesStatus CPrvs
JOIN CPResultWithRefAndContext CPrrc
  ON CPrvs.company = CPrrc.company
 AND CPrvs.idControlProcedureResult = CPrrc.idControlProcedureResult
JOIN ManufacturingReference MR
  ON MR.company = CPrvs.company AND MR.manufacturingReference = CPrvs.manufacturingReference
WHERE CPrvs.company = @company
  AND CPrvs.controlOperationType = 1
  AND CPrrc.executionDate >= @from AND CPrrc.executionDate < @to
  AND (TRY_CONVERT(float, CPrvs.minTolerance) IS NOT NULL OR TRY_CONVERT(float, CPrvs.maxTolerance) IS NOT NULL)
GROUP BY MR.manufacturingReferenceName, CPrvs.controlOperationName;";

            using var conn = new SqlConnection(cs);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@company", company);
            cmd.Parameters.AddWithValue("@from", fromUtc);
            cmd.Parameters.AddWithValue("@to", toUtc);

            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                var part = rd.GetString(0);
                var test = rd.GetString(1);
                var lsl = rd.IsDBNull(2) ? double.NaN : rd.GetDouble(2);
                var usl = rd.IsDBNull(3) ? double.NaN : rd.GetDouble(3);
                var tgt = rd.IsDBNull(4) ? double.NaN : rd.GetDouble(4);

                // Si no hay target, usa el centro del intervalo
                if (double.IsNaN(tgt) && !double.IsNaN(lsl) && !double.IsNaN(usl))
                    tgt = (lsl + usl) / 2.0;

                if (!double.IsNaN(lsl) && !double.IsNaN(usl))
                    dict[(part, test)] = new Spec(lsl, usl, tgt);
            }
            return dict;
        }

        // =========  CÁLCULOS  =========
        private static List<CapabilityRow> ComputeCapability(
            List<AggRow> aggRows, List<SgRow> sgRows,
            Dictionary<(string Part, string Test), Spec> specs)
        {
            var outList = new List<CapabilityRow>();

            // Índice del detalle por Part/Process/Test
            var sgIndex = sgRows.GroupBy(s => (s.Part, s.Process, s.Test))
                                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SubgroupTs).ToList());

            foreach (var a in aggRows)
            {
                specs.TryGetValue((a.Part, a.Test), out var sp);

                if (sp == null || sp.USL <= sp.LSL)
                {
                    outList.Add(new CapabilityRow(a.Part, a.Process, a.Test, a.Subgroups, a.Mean,
                        null, null, null, "Sin LSL/USL/Target"));
                    continue;
                }

                double? sigmaWithin = null;
                string nota = "";

                // Caso 1: n constante >= 2 (exacto Xbar-R)
                if (a.n_min == a.n_max && a.n_max >= 2 && a.Rbar > 0 && D2.TryGetValue(a.n_max, out var d2))
                {
                    sigmaWithin = a.Rbar / d2;
                }
                // Caso 2: n variable pero >=2 (aprox con n promedio)
                else if (a.n_min >= 2 && a.Rbar > 0)
                {
                    int nAvg = (int)Math.Round((double)a.Nobs / Math.Max(1, a.Subgroups));
                    // elige d2 más cercano disponible
                    int nClosest = D2.Keys.OrderBy(k => Math.Abs(k - nAvg)).FirstOrDefault();
                    if (nClosest >= 2)
                    {
                        sigmaWithin = a.Rbar / D2[nClosest];
                        nota = $"σ≈R̄/d₂(n≈{nClosest})";
                    }
                }
                // Caso 3: n = 1 (sin detalle sg no hay I-MR)
                else if (a.n_max == 1)
                {
                    nota = "n=1 requiere detalle I-MR";
                }

                if (sigmaWithin is null || sigmaWithin <= 0)
                {
                    outList.Add(new CapabilityRow(a.Part, a.Process, a.Test, a.Subgroups, a.Mean,
                        null, null, null, string.IsNullOrEmpty(nota) ? "No se pudo estimar σ_within" : nota));
                    continue;
                }


                var meanDelta = (double.IsNaN(sp.Target) ? (double?)null : a.Mean - sp.Target);
                var cpu = (sp.USL - a.Mean) / (3.0 * sigmaWithin.Value);
                var cpl = (a.Mean - sp.LSL) / (3.0 * sigmaWithin.Value);
                var cp = (sp.USL - sp.LSL) / (6.0 * sigmaWithin.Value);
                var cpk = Math.Min(cpu, cpl);

                outList.Add(new CapabilityRow(a.Part, a.Process, a.Test, a.Subgroups, a.Mean,
     meanDelta, cp, cpk, nota));

            }

            return outList;
        }
    }
}
