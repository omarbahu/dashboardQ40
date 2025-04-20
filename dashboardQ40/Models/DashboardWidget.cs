using System.Text.Json.Serialization;

namespace dashboardQ40.Models
{
    public class DashboardWidget
    {
        public int WidgetID { get; set; }
        public int TemplateID { get; set; }
        public string VariableX { get; set; }  // 🔹 Asegurar que sea "VariableX"
        public string WidgetType { get; set; }
        public string Position { get; set; }
        public string Config { get; set; }
        public string DataSource { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DashboardTemplate Template { get; set; }
    }
}
