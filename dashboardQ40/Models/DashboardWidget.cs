using System.Text.Json.Serialization;

namespace dashboardQ40.Models
{
    public class DashboardWidget
    {
        public int WidgetID { get; set; }
        public int TemplateID { get; set; }
        public string VariableX { get; set; } = string.Empty; // 🔹 Asegurar que sea "VariableX"
        public string WidgetType { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Config { get; set; } = string.Empty;
        public string DataSource { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DashboardTemplate Template { get; set; }
    }
}
