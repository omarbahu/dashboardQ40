using static dashboardQ40.Services.AuditTrazabilityClass;

namespace dashboardQ40.Services
{
    public class AuditHistorialService
    {
        public static List<HistorialReporteItem> ObtenerHistorial(
         IConfiguration cfg, string connStr, string queryKey, string q, string company)
        {
            var sql = cfg[$"ExcelToSqlMappings:{queryKey}:Query"];
            var p = new Dictionary<string, object> { { "@q", q ?? "" }, { "@company", company ?? "" } };

            return DynamicSqlService.EjecutarQuery<HistorialReporteItem>(queryKey, p, null, cfg, connStr)
                   ?? new List<HistorialReporteItem>();
        }

        public static HistorialReporteLookup ObtenerLoteHoraQuejaPorId(
            IConfiguration cfg, string connStr, string queryKey, int id)
        {
            var sql = cfg[$"ExcelToSqlMappings:{queryKey}:Query"];
            var p = new Dictionary<string, object> { { "@id", id } };
            return DynamicSqlService.EjecutarQuery<HistorialReporteLookup>(queryKey, p, null, cfg, connStr)?.FirstOrDefault();
        }
    }
}
