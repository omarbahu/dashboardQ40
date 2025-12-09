using System.Globalization;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Models
{
    public class Models
    {
        public class DashboardTemplateCreateModel
        {
            public string TemplateName { get; set; } = string.Empty;
            public string Planta { get; set; } = string.Empty;
            public string Linea { get; set; } = string.Empty;
            public string VariableY { get; set; } = string.Empty;
            public List<DashboardWidgetCreateModel> Widgets { get; set; } = new List<DashboardWidgetCreateModel>();
        }

        public class DashboardWidgetCreateModel
        {
            public string VariableX { get; set; } = string.Empty; // 🔹 Asegurar que use "VariableX"
            public string WidgetType { get; set; } = string.Empty;
            public string Position { get; set; } = string.Empty;
            public string Config { get; set; } = string.Empty;
            public string DataSource { get; set; } = string.Empty;
        }

        public class DashboardWidgetDTO
        {
            public string VariableX { get; set; } = string.Empty;
            public string WidgetType { get; set; } = string.Empty;
            public string Position { get; set; } = string.Empty;
        }

        public class result_token
        {
            public string access_token { get; set; } = string.Empty;
            public int expires_in { get; set; }
            public string token_type { get; set; } = string.Empty;
        }

        public class credenciales_token
        {
            public string userName { get; set; } = string.Empty;
            public string password { get; set; } = string.Empty;
        }

        public class result_Q_Lineas
        {
            public string query { get; set; } = string.Empty;
            public List<result_lineas> result { get; set; }

        }
        public class result_lineas
        {

            public string workplace { get; set; } = string.Empty;
            public string workplaceName { get; set; } = string.Empty;
            public string workMode { get; set; } = string.Empty;

        }

        public class result_QResNormLi
        {
            public string? query { get; set; } = string.Empty;
            public object? parameters { get; set; }
            public List<raw_QResNormLi> result { get; set; } = new();
        }

        public class raw_QResNormLi
        {
            public string? controlprocedure { get; set; } = string.Empty;
            public string? controlOperation { get; set; } = string.Empty;
            public string? controlOperationName { get; set; } = string.Empty;
            public double? resultValue { get; set; }
            public double? minTolerance { get; set; }
            public double? maxTolerance { get; set; }
            public DateTime? executionDate { get; set; }
            public string? manufacturingReference { get; set; } = string.Empty; // SKU
        }

        public class result_Q_Companies
        {
            public string query { get; set; } = string.Empty;
            public List<result_companies> result { get; set; }

        }

        public class result_companies
        {

            public string company { get; set; } = string.Empty;
            public string companyName { get; set; } = string.Empty;
            public string culture { get; set; } = string.Empty;
            public string countryCode { get; set; } = string.Empty;
            public CultureInfo cultureinfo { get; set; }
            public RegionInfo regioninfo { get; set; }

        }

        public class result_Q_MateriaPrima
        {
            public string query { get; set; } = string.Empty;
            public List<result_MateriaPrima> result { get; set; }
        }

        public class result_MateriaPrima
        {
            public string referenceMovement { get; set; } = string.Empty;
            public string supplier { get; set; } = string.Empty;
            public string supplierBatch { get; set; } = string.Empty;
            public DateTime? issueDate { get; set; }
            public int? realQuantityInParcel { get; set; }
            public CultureInfo cultureinfo { get; set; }
            public RegionInfo regioninfo { get; set; }
        }

        //SELECT referenceMovement, supplier, supplierBatch, issueDate, realQuantityInParcel FROM EntryFromProvider WHERE company = @company AND referenceMovement IN ({lotes})

        public class CompanyOption
        {
            public string Company { get; set; } = string.Empty;      // "001"
            public string CompanyName { get; set; } = string.Empty;  // "Planta Insurgentes"
            public string Culture { get; set; } = string.Empty;      // "es-MX"
            public string CountryCode { get; set; } = string.Empty;  // "MX"
        }


        public class VariablesYConfig
        {
            public Dictionary<string, string> VariablesY { get; set; } = new Dictionary<string, string>();
        }

        public class result_Q_Productos
        {
            public string query { get; set; } = string.Empty;
            public List<result_productos> result { get; set; }

        }

        public class result_productos
        {
            public string manufacturingOrder { get; set; } = string.Empty;
            public string manufacturingReferenceName { get; set; } = string.Empty;
        }

        public class result_Q_Families
        {
            public string query { get; set; } = string.Empty;
            public List<result_Families> result { get; set; }

        }

        public class result_Families
        {
            public string manufacturingFamily { get; set; } = string.Empty;
            public string manufacturingFamilyName { get; set; } = string.Empty;
        }

        public class result_Q_VarY
        {
            public string query { get; set; } = string.Empty;
            public List<result_varY> result { get; set; }

        }

        public class result_varY
        {
            public int contador{ get; set; }
            public string controlOperation { get; set; } = string.Empty;
            public string controlOperationName { get; set; } = string.Empty;
        }

        public class result_Q_Resultados
        {
            public string query { get; set; } = string.Empty;
            public List<result_Resultados> result { get; set; }

        }

        public class result_Resultados
        {
            public string controlOperation { get; set; } = string.Empty;
            public string controlOperationName { get; set; } = string.Empty;
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
            public long BatchCreation { get; set; }
            public string manufacturingReference { get; set; } = string.Empty;
            public double quantity { get; set; }
            public string workplace { get; set; } = string.Empty;
            public double initialQuantity { get; set; }
        }

        public class result_Q_authUser
        {
            public string query { get; set; } = string.Empty;
            public List<result_authUser> result { get; set; }

        }

        public class result_authUser
        {
            public string appUser { get; set; } = string.Empty;
            public string appUserName { get; set; } = string.Empty;
            public string role { get; set; } = string.Empty;
            public string culture { get; set; } = string.Empty;
            public string company { get; set; } = string.Empty;

            // OJO: aquí como string, porque en el JSON viene "101"
            public string programGroup { get; set; } = string.Empty;
            public string programGroupName { get; set; } = string.Empty;

            public bool canread { get; set; }
            public bool caninsert { get; set; }
            public bool canmodify { get; set; }
            public bool candelete { get; set; }
        }


        public class DashboardProgramPermission
        {
            public string ProgramGroup { get; set; }
            public string ProgramGroupName { get; set; } = string.Empty;

            public bool Global { get; set; }
            public bool Country { get; set; }
            public bool Planta { get; set; }
            public bool Modify { get; set; }
        }

        /*
         CPrvs.controlOperation, CPrvs.controlOperationName, 
CPrvs.resultValue, CPrvs.minTolerance, CPrvs.maxTolerance, CPrrc.executionDate
        */

        public class SqlMappingConfig
        {
            public string Query { get; set; } = string.Empty;
            public Dictionary<string, string> ColumnMappings { get; set; }
            public string DescripcionField { get; set; } = string.Empty; // campo para el join con el diccionario de descripciones
        }


        public sealed class YRawRow
        {
            public string controlOperation { get; set; } = "";
            public string controlOperationName { get; set; } = "";
            public DateTime executionDate { get; set; }
            public double? resultValue { get; set; }
            public double? minTolerance { get; set; }
            public double? maxTolerance { get; set; }
        }

        public sealed class YSummary
        {
            public string Codigo { get; set; } = "";
            public string Nombre { get; set; } = "";
            public int Tests { get; set; }
            public int CoverageDays { get; set; }
            public int TotalDays { get; set; }
            public int OOS { get; set; }
            public double? Mean { get; set; }
            public DateTime? LastTs { get; set; }
            public double? LastValue { get; set; }
            // opcional: public List<(DateTime d, double mean)> Spark { get; set; } = new();

            public List<double> Spark { get; set; } = new();
        }

        public sealed class ResultEnvelope<T>
        {
            public T result { get; set; }
        }

        public class ACPayloadRow
        {
            public string? manufacturingOrder { get; set; }
            public string? manufacturingPhase { get; set; }
            public string? batch { get; set; }
            public string? idControlProcedureResult { get; set; }
            public string? isManual { get; set; }
            public string? workplace { get; set; }
            public string? launchingDate { get; set; }
            public string? controlProcedure { get; set; }
            public string? controlProcedureVersion { get; set; }
            public string? controlProcedureLevel { get; set; }
            public string? controlProcedureNote { get; set; }
            public string? worker { get; set; }
            public string? controlOperation { get; set; }
            public int SampleSize { get; set; }            
            public int position { get; set; }
            public string? controlOperationNote { get; set; }
            public string? doesNotApply { get; set; }
            public string? resultNumber { get; set; }
            public string? resultAttribute { get; set; }
            public string? resultValue { get; set; }
            public string? resultPresetAttributeValue { get; set; }
            public string? controlProcedureLevelName { get; set; }            
            public string? controlOperationResultValueNote { get; set; }
            public string controlOperationType { get; set; }
        }


        public class RowsRequest
        {
            public string? company { get; set; }
            public string? workplace { get; set; }
            public string? manufacturingorder { get; set; }
            public string? startdate { get; set; } // "yyyy-MM-dd"
            public string? enddate { get; set; }   // "yyyy-MM-dd"
        }

        public class AutocontrolExcelRow
        {
            // Campos raíz del JSON
            public string Batch { get; set; }
            public string ManufacturingOrder { get; set; }
            public string ManufacturingPhase { get; set; }
            public string IdControlProcedureResult { get; set; }
            public string IsManual { get; set; }
            public string Workplace { get; set; }
            public string LaunchingDate { get; set; }
            public string ControlProcedure { get; set; }
            public string ControlProcedureVersion { get; set; }
            public string ControlProcedureLevel { get; set; }
            public string ControlProcedureNote { get; set; }
            public string Worker { get; set; }

            // Campos de operación / valores
            public string ControlOperation { get; set; }
            public string ControlOperationNote { get; set; }
            public string DoesNotApply { get; set; }

            public string ResultAttribute { get; set; }               // luego lo mapeamos a resultAtribute
            public string ResultNumber { get; set; }
            public string ResultValue { get; set; }
            public string ResultPresetAttributeValue { get; set; }
            public string ControlOperationResultValueNote { get; set; }
            public string controlOperationType { get; set; }

        }

        public class CapabilityRowDto
        {
            public string VariableName { get; set; }   // "CONTENIDO NETO - CCL SC 600ML"
            public int N { get; set; }                 // n
            public double? Mean { get; set; }          // Media
            public double? SigmaGlobal { get; set; }   // σ Global (o la que uses en el certificado)
            public double? LSL { get; set; }           // LSL
            public double? USL { get; set; }           // USL
            public double? Cpk { get; set; }           // Cpk
            public double? PctBelowLsl { get; set; }   // % bajo LEI
            public double? PctAboveUsl { get; set; }   // % sobre LES
                                                       // Si tu DTO trae Cp, Pp, Ppk y luego los quieres mostrar,
                                                       // aquí también los agregamos.
        }

    }
}
