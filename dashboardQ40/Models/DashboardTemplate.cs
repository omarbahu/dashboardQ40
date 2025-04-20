namespace dashboardQ40.Models
{
    public class DashboardTemplate
    {
        public int TemplateID { get; set; }
        public string TemplateName { get; set; }
        public string Planta { get; set; } // Nueva columna
        public string Linea { get; set; } // Nueva columna
        public string VariableY { get; set; } // Nueva columna
        public string CreatedBy { get; set; } // Usuario administrador que lo creó
        public DateTime CreatedAt { get; set; }

        public List<DashboardWidget> Widgets { get; set; } = new List<DashboardWidget>();
    }

}
