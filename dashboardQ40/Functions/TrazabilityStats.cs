using dashboardQ40.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;


namespace dashboardQ40.Functions
{
    public class TrazabilityStats
    {
        public class StatResult
        {
            public string Batch { get; set; } = "";
            public string Variable { get; set; } = "";
            public int Conteo { get; set; }
            public int Defectos { get; set; }
            public double Media { get; set; }
            public double Sigma { get; set; }
            public double Cp { get; set; }
            public double Cpk { get; set; }
        }

        public static List<VariableEstadistica> CalcularEstadisticas(DataTable checklist)
        {
            var resultados = new List<VariableEstadistica>();
            if (checklist == null || checklist.Rows.Count == 0)
                return resultados;

            var valoresPorGrupo = new Dictionary<string, List<double>>();
            var toleranciasPorGrupo = new Dictionary<string, (double minTol, double maxTol)>();

            foreach (DataRow row in checklist.Rows)
            {
                if (row["resultValue"] == DBNull.Value) continue;

                string lote = row["batch"].ToString();
                string variable = row["controlOperationName"].ToString();
                string key = lote + "|" + variable;

                if (!valoresPorGrupo.ContainsKey(key))
                {
                    valoresPorGrupo[key] = new List<double>();
                    double minTol = row["minTolerance"] != DBNull.Value ? Convert.ToDouble(row["minTolerance"]) : 0;
                    double maxTol = row["maxTolerance"] != DBNull.Value ? Convert.ToDouble(row["maxTolerance"]) : 0;
                    toleranciasPorGrupo[key] = (minTol, maxTol);
                }

                valoresPorGrupo[key].Add(Convert.ToDouble(row["resultValue"]));
            }

            foreach (var kvp in valoresPorGrupo)
            {
                string[] partes = kvp.Key.Split('|');
                string lote = partes[0];
                string variable = partes[1];

                List<double> valores = kvp.Value;
                double minTol = toleranciasPorGrupo[kvp.Key].minTol;
                double maxTol = toleranciasPorGrupo[kvp.Key].maxTol;

                double sigma = valores.Count > 1 ? CalcularDesviacionEstandar(valores) : 0;
                double media = valores.Count > 0 ? CalcularPromedio(valores) : 0;
                double cp = sigma > 0 ? (maxTol - minTol) / (6 * sigma) : 0;
                double cpk = CalcularCpk(valores, minTol, maxTol);
                int defectos = valores.FindAll(v => v < minTol || v > maxTol).Count;

                resultados.Add(new VariableEstadistica
                {
                    Lote = lote,
                    Variable = variable,
                    Conteo = valores.Count,
                    Media = media,
                    Sigma = sigma,
                    LSL = minTol,
                    USL = maxTol,
                    Cp = cp,
                    Cpk = cpk,
                    Defectos = defectos
                });
            }

            return resultados;
        }

        private static double CalcularPromedio(List<double> valores)
        {
            double suma = 0;
            foreach (var v in valores)
                suma += v;
            return suma / valores.Count;
        }

        private static double CalcularDesviacionEstandar(List<double> valores)
        {
            if (valores.Count <= 1) return 0;
            double promedio = CalcularPromedio(valores);
            double sumaCuadrados = 0;
            foreach (var v in valores)
                sumaCuadrados += Math.Pow(v - promedio, 2);
            return Math.Sqrt(sumaCuadrados / (valores.Count - 1));
        }

        private static double CalcularCpk(List<double> valores, double lsl, double usl)
        {
            if (valores.Count <= 1) return 0;
            double media = CalcularPromedio(valores);
            double sigma = CalcularDesviacionEstandar(valores);
            if (sigma == 0) return 0;
            double cpu = (usl - media) / (3 * sigma);
            double cpl = (media - lsl) / (3 * sigma);
            return Math.Min(cpu, cpl);
        }
    }
}

