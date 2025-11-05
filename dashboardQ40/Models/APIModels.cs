namespace dashboardQ40.Models
{
    public class APIModels
    {
        public class TemplateModel
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
        }

        // Modelo para un widget
        public class WidgetModel
        {
            public int TemplateId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }
    }
}
