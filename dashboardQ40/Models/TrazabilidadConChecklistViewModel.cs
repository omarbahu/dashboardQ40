using System.Data;

namespace dashboardQ40.Models
{
    public class TrazabilidadConChecklistViewModel
    {
        public DataTable Trazabilidad { get; set; }
        public DataTable Checklist { get; set; }
        public Dictionary<string, VariableEstadistica> Estadisticas { get; set; }
        public string SearchBatch { get; set; } = string.Empty;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
