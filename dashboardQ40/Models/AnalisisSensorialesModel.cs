using static dashboardQ40.Models.Models;

namespace dashboardQ40.Models
{
    public class AnalisisSensorialesModel
    {
        public class result_Q_BatchExtraRowDto
        {
            public string query { get; set; } = string.Empty;
            public List<BatchExtraRowDto> result { get; set; }

        }
        public class BatchExtraRowDto
        {
            public int Batch { get; set; }                       // 258
            public string ManufacturingReference { get; set; }   // LIBSEN
            public string BatchIdentifier { get; set; }          // "1"
            public DateTime ImputationDate { get; set; }         // 2025-11-19
            public DateTime StartDate { get; set; }              // 2025-10-31 17:00:19.033
            public string Workplace { get; set; }                // LIBSEN

            public string ExtraField { get; set; }               // IDMTRA#1, PROD#1, ...
            public string StringValue { get; set; }              // 172, COCA, 17MAY26-5, 16:29, ...
            public int Position { get; set; }                    // 1..40
        }

   

        public class result_Q_AutoControlRowDto
        {
            public string query { get; set; } = string.Empty;
            public List<AutoControlRowDto> result { get; set; }
        }

        public class AutoControlRowDto
        {
            public string ControlProcedure { get; set; }        // SEN-0001
            public int ControlProcedureLevel { get; set; }      // 1, 2, 3 (panelista)

            public string Worker { get; set; }                  // <-- CAMBIAR a string
            public string WorkerName { get; set; }

            public string ControlOperation { get; set; }
            public string ControlOperationName { get; set; }

            public string ResultPresetAttributeValue { get; set; }
            public int? IsValueOk { get; set; }

            public int? ResultAttribute { get; set; }           // 1 = IN, !=1 = OUT (cuando type=2)
            public int? ControlOperationType { get; set; }

        }


        public class LoteExtraPorNumero
        {
            public int Numero { get; set; }         // 1..10

            public string IdMuestra { get; set; }   // de IDMTRA#n
            public string Producto { get; set; }    // de PROD#n
            public string LoteBotella { get; set; } // de LOTE#n
            public string Hora { get; set; }        // de HORA#n
        }

        public class AutoControlPorNumero
        {
            public string ControlProcedure { get; set; }
            public int ControlProcedureLevel { get; set; }

            public string Worker { get; set; }
            public string WorkerName { get; set; }

            public int Numero { get; set; }               // 1..10

            public string NombreProducto { get; set; }    // "COCA", "MANZANA", ...
            public string ObsSabor { get; set; }          // texto
            public string SaborInOut { get; set; }        // IN / OUT / NULL
            public string ObsOlor { get; set; }           // texto
            public string OlorInOut { get; set; }         // IN / OUT / NULL
            public string AparienciaInOut { get; set; }   // IN / OUT / NULL
        }

        public class ReporteSensorialFila
        {
            // Datos de batch (comunes a las 10 filas)
            public int Batch { get; set; }
            public string ManufacturingReference { get; set; }
            public string BatchIdentifier { get; set; }
            public DateTime ImputationDate { get; set; }
            public DateTime StartDate { get; set; }
            public string Workplace { get; set; }

            // Panelista
            public string ControlProcedure { get; set; }
            public int ControlProcedureLevel { get; set; }
            public string Worker { get; set; }
            public string WorkerName { get; set; }

            // Número de muestra (1..10)
            public int Numero { get; set; }

            // Extra de lote
            public string IdMuestra { get; set; }
            public string ProductoExtra { get; set; }
            public string LoteBotella { get; set; }
            public string Hora { get; set; }

            // Resultado sensorial
            public string NombreProducto { get; set; }
            public string ObsSabor { get; set; }
            public string SaborInOut { get; set; }
            public string ObsOlor { get; set; }
            public string OlorInOut { get; set; }
            public string AparienciaInOut { get; set; }
        }


        public class AnalisisSensorialRequest
        {
            public string startDate { get; set; }   // viene del input datetime-local
            public string endDate { get; set; }
            public string company { get; set; }     // planta seleccionada (company)

            public string? productCode { get; set; }
            public string? productHour { get; set; }
        }
    }
}
