namespace dashboardQ40.Models
{
    public class Models
    {
        public class DashboardTemplateCreateModel
        {
            public string TemplateName { get; set; }
            public string Planta { get; set; }
            public string Linea { get; set; }
            public string VariableY { get; set; }
            public List<DashboardWidgetCreateModel> Widgets { get; set; } = new List<DashboardWidgetCreateModel>();
        }

        public class DashboardWidgetCreateModel
        {
            public string VariableX { get; set; }  // 🔹 Asegurar que use "VariableX"
            public string WidgetType { get; set; }
            public string Position { get; set; }
            public string Config { get; set; }
            public string DataSource { get; set; }
        }

        public class DashboardWidgetDTO
        {
            public string VariableX { get; set; }
            public string WidgetType { get; set; }
            public string Position { get; set; }
        }

        public class result_token
        {
            public string access_token { get; set; }
            public int expires_in { get; set; }
            public string token_type { get; set; }
        }

        public class credenciales_token
        {
            public string userName { get; set; }
            public string password { get; set; }
        }

        public class result_Q_Lineas
        {
            public string query { get; set; }
            public List<result_lineas> result { get; set; }

        }
        public class result_lineas
        {

            public string workplace { get; set; }
            public string workplaceName { get; set; }
            public string workMode { get; set; }

        }

        public class VariablesYConfig
        {
            public Dictionary<string, string> VariablesY { get; set; } = new Dictionary<string, string>();
        }

        public class result_Q_Productos
        {
            public string query { get; set; }
            public List<result_productos> result { get; set; }

        }

        public class result_productos
        {
            public string manufacturingOrder { get; set; }
            public string manufacturingReferenceName { get; set; }
        }

        public class result_Q_VarY
        {
            public string query { get; set; }
            public List<result_varY> result { get; set; }

        }

        public class result_varY
        {
            public string controlOperation { get; set; }
            public string controlOperationName { get; set; }
        }

        public class result_Q_Resultados
        {
            public string query { get; set; }
            public List<result_Resultados> result { get; set; }

        }

        public class result_Resultados
        {
            public string controlOperation { get; set; }
            public string controlOperationName { get; set; }
            public double? resultValue { get; set; }  // Ahora puede ser nulo
            public double? minTolerance { get; set; } // Ahora puede ser nulo
            public double? maxTolerance { get; set; } // Ahora puede ser nulo
            public DateTime executionDate { get; set; }

        }

        public class BatchInfo
        {
            public int BatchId { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
        }

        public class result_Q_authUser
        {
            public string query { get; set; }
            public List<result_authUser> result { get; set; }

        }

        public class result_authUser
        {

            public string appUser { get; set; }
            public string appUserName { get; set; }
            public string culture { get; set; }

        }

        /*
         CPrvs.controlOperation, CPrvs.controlOperationName, 
CPrvs.resultValue, CPrvs.minTolerance, CPrvs.maxTolerance, CPrrc.executionDate
        */
    }
}
