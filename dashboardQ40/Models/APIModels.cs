namespace dashboardQ40.Models
{
    public class APIModels
    {
        public class TemplateModel
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }

        // Modelo para un widget
        public class WidgetModel
        {
            public int TemplateId { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
        }
    }
}
