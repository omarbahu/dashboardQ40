using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;
using static dashboardQ40.Models.Models;
using static dashboardQ40.Services.common;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Data;
using System.Text.Json;

using dashboardQ40.Models;

namespace dashboardQ40.Services
{
    public class AuditTrazabilityClass
    {
      
        public static List<RegistroMateriaPrima> ObtenerDatosMateriaPrima(string company, List<long> lotes, Dictionary<long, LoteDescripcionInfo> descripciones, string connectionString)
        {
            var resultados = new List<RegistroMateriaPrima>();

            // Diccionario para registrar qué lotes sí se encontraron
            var encontrados = new HashSet<long>();

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string lotesInClause = string.Join(",", lotes.Select(l => l.ToString()));

                string query = $@"
            SELECT 
                referenceMovement,
                supplier,
                supplierBatch,
                issueDate,
                realQuantityInParcel,
                entryFromProvider
            FROM [Captor4ArcaPro].[dbo].[EntryFromProvider]
            WHERE company = @company AND referenceMovement IN ({lotesInClause})
        ";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@company", company);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long loteId = Convert.ToInt64(reader["referenceMovement"]);
                            encontrados.Add(loteId);

                            resultados.Add(new RegistroMateriaPrima
                            {
                                Descripcion = descripciones.ContainsKey(loteId)
        ? descripciones[loteId].ManufacturingReferenceName
        : "Sin descripción",

                                Proveedor = reader["supplier"]?.ToString() ?? "",
                                LoteExterno = reader["supplierBatch"]?.ToString() ?? "",

                                // LoteInterno usa el BatchIdentifier
                                LoteInterno = descripciones.ContainsKey(loteId)
        ? descripciones[loteId].BatchName
        : loteId.ToString(), // fallback si no está
                                FechaRecepcion = reader["issueDate"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["issueDate"]).ToString("dd/MM/yyyy")
                                    : "",
                                Cantidad = reader["realQuantityInParcel"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }

            // Agregar los que NO se encontraron
            foreach (var kvp in descripciones)
            {
                if (!encontrados.Contains(kvp.Key))
                {
                    resultados.Add(new RegistroMateriaPrima
                    {
                        Descripcion = kvp.Value.ManufacturingReferenceName,
                        Proveedor = "",
                        LoteExterno = "",
                        LoteInterno = kvp.Value.BatchName, // usa BatchIdentifier aquí
                        FechaRecepcion = "",
                        Cantidad = ""
                    });
                }
            }

            return resultados;
        }

        public static async Task<int> GuardarReporteAsync(ReporteTrazabilidad m, string connectionString)
        {
            // Toma los tipos tal cual vienen del DTO
            var fechaHora = m.FechaHora;                  // DateTime
            var horaInicio = m.HoraInicio;                 // TimeSpan?
            var horaFin = m.HoraFin;                    // TimeSpan?
            var horaQueja = (TimeSpan?)m.horaQueja;       // TimeSpan (lo vuelvo nullable para DB)

            // JSON flexible
            var extra = new
            {
                motivo = m.MotivoTrazabilidad,
                porcEfic = m.PorcEficProductoTerminado,
                usuarioVobo = m.UsuarioVobo
            };
            var extraJson = JsonSerializer.Serialize(extra);

            const string sql = @"
INSERT INTO dbo.reporte_trazabilidad
(
    country_code, plant_code, fecha_hora, hora_inicio, hora_fin,
    motivo_trazabilidad, traza_producto_mp, porc_efic_producto_terminado,
    lote, revision, usuario_vobo, hora_queja, [status], extra_data
)
VALUES
(
    @country, @plant, @fecha_hora, @hora_inicio, @hora_fin,
    @motivo, @traza_mp, @porc_efic,
    @lote, @revision, @usuario_vobo, @hora_queja, @status, @extra
);
SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var cn = new SqlConnection(connectionString);
            await cn.OpenAsync();

            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@country", (object?)m.country ?? DBNull.Value);  // acepta null si aún no lo mandas
            cmd.Parameters.AddWithValue("@plant", (object?)m.company ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fecha_hora", fechaHora);
            cmd.Parameters.AddWithValue("@hora_inicio", horaInicio);
            cmd.Parameters.AddWithValue("@hora_fin", horaFin);
            cmd.Parameters.AddWithValue("@motivo", (object?)m.MotivoTrazabilidad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@traza_mp", m.TrazaProductoMp);
            cmd.Parameters.AddWithValue("@porc_efic", (object?)m.PorcEficProductoTerminado ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lote", (object?)m.Lote ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@revision", (object?)m.Revision ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@usuario_vobo", (object?)m.UsuarioVobo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hora_queja", horaQueja);
            cmd.Parameters.AddWithValue("@status", "Draft"); // o "Final", como decidas
            cmd.Parameters.AddWithValue("@extra", (object?)extraJson ?? DBNull.Value);

            var newId = (int)await cmd.ExecuteScalarAsync();
            return newId;
        }


        public static BloqueProductoTerminadoModel ObtenerDatosProductoTerminado(string lote)
        {
            return new BloqueProductoTerminadoModel
            {
                DescripcionProducto = "Joya Durazno 1.5 L",
                CodigoProducto = "23DIC24-3.701.22.26",
                EncargadoPruebas = "Karen Choul Garza",
                SupervisorJarabes = "Yahaira Luna",
                VacioJarabeTerminado = "Milton / Cristian",
                SupervisorCalidad = "Marco Robles",
                FechaProduccion = "15/10/2024 al 16/10/2024",
                InicioProduccion = "15/10/2024 21:30",
                FinProduccion = "16/10/2024 07:51",
                SupervisorProduccion = "Ismael Lara",
                SupervisorMantenimiento = "Ismael Lara",
                Llenadora = "3",
                CantidadElaborada = "19,933",
                NumeroLinea = "3"
            };
        }

        public static BloqueEntregaInformacionModel ObtenerEntregaInformacion(string lote)
        {
            return new BloqueEntregaInformacionModel
            {
                Registros = new List<RegistroEntregaInformacionModel>
                {
                    new() { NombrePersona = "GARZA ESPARZA JULIO CÉSAR", Hora = "09:11", Puesto = "Planeador", Area = "Producción", Actividad = "Cantidad de material producida", MedioEntrega = "Correo electrónico", Observaciones = "Ninguna" },
                    new() { NombrePersona = "RANGEL CASTILLO VIASIT BERENICE", Hora = "13:18", Puesto = "Supervisor", Area = "Jarabes y saneamiento", Actividad = "Datos de elaboración de jarabe y limpieza y saneamiento", MedioEntrega = "Correo electrónico", Observaciones = "Ninguna" },
                    // ... y así los demás registros
                }
            };
        }

        public static BloqueLotesPrincipalesModel ObtenerJarabeSimplePorLote(string lote)
        {
            return new BloqueLotesPrincipalesModel
            {
                TituloBloque = "Lote de Jarabe Simple (Núm. Batch)",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO" },
                Registros = new List<RegistroLotesPrincipales>
        {
            new() { DescripcionCampo = "LOTE DE JARABE SIMPLE (NÚM. BATCH):", ValoresPorSku = new() { "JSB1001", "JSB0998", "JSB1000", "JSB0999", "JSB1002" } },
            new() { DescripcionCampo = "# TANQUE DONDE SE ALMACENO EL JARABE SIMPLE:", ValoresPorSku = new() { "TQ-01", "TQ-02", "TQ-03", "TQ-04", "TQ-05" } },
            new() { DescripcionCampo = "TIPO DE AZUCAR UTILIZADA:", ValoresPorSku = new() { "Mascabado", "Mascabado", "Refinada", "Refinada", "Refinada" } },
            new() { DescripcionCampo = "LOTE DE AZUCAR:", ValoresPorSku = new() { "AZ1009", "AZ1007", "AZ1010", "AZ1005", "AZ1012" } },
            new() { DescripcionCampo = "FECHA DE ELABORACION DEL JARABE SIMPLE:", ValoresPorSku = new() { "15/10/24", "14/10/24", "15/10/24", "14/10/24", "13/10/24" } },
            new() { DescripcionCampo = "VOLUMEN PREPARADO DE JARABE SIMPLE:", ValoresPorSku = new() { "1,200 L", "1,000 L", "1,100 L", "950 L", "980 L" } },
            new() { DescripcionCampo = "VOLUMEN UTILIZADO DE JARABE SIMPLE:", ValoresPorSku = new() { "1,100 L", "900 L", "1,000 L", "850 L", "920 L" } },
            new() { DescripcionCampo = "# TANQUE DISOLUTOR DONDE SE PREPARO EL JARABE SIMPLE:", ValoresPorSku = new() { "D1", "D2", "D3", "D4", "D5" } },
            new() { DescripcionCampo = "LOTE DE AGUA TRATADA UTILIZADA JAR. SIMPLE:", ValoresPorSku = new() { "AG1001", "AG1002", "AG1003", "AG1004", "AG1005" } },
            new() { DescripcionCampo = "LOTE DEL MATERIAL FILTRANTE ( SOLKAFLOC, POLIACRILAMIDA,CELATOM, ARBOCEL):", ValoresPorSku = new() { "MF101", "MF102", "MF103", "MF104", "MF105" } },
            new() { DescripcionCampo = "LOTE DE FILTRO BOLSA UTILIZADO:", ValoresPorSku = new() { "FB101", "FB102", "FB103", "FB104", "FB105" } },
            new() { DescripcionCampo = "VOLUMEN PREPARADO:", ValoresPorSku = new() { "1,200 L", "1,000 L", "1,150 L", "950 L", "970 L" } },
        }
            };
        }


        public static BloqueLotesPrincipalesModel ObtenerJarabeSimpleConContacto(string lote)
        {
            return new BloqueLotesPrincipalesModel
            {
                TituloBloque = "Lote de Jarabe Simple (Núm. Batch). Mencionar lote correspondiente a la muestra y 2 anteriores que pudieron haber estado en contacto",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO" },

                Registros = new List<RegistroLotesPrincipales>
                { 
       /* new() { DescripcionCampo = "LOTE DE JARABE SIMPLE (NÚM. BATCH):", ValoresPorSku = new() { "JS001", "JS000", "JS002", "JS003", "JS004", "JS005" } },
        new() { DescripcionCampo = "# TANQUE DONDE SE ALMACENO EL JARABE SIMPLE:", ValoresPorSku = new() { "T01", "T01", "T02", "T03", "T04", "T05" } },
        new() { DescripcionCampo = "TIPO DE AZUCAR UTILIZADA:", ValoresPorSku = new() { "Refinada", "Refinada", "Mascabado", "Refinada", "Orgánica", "Refinada" } },
        new() { DescripcionCampo = "LOTE DE AZUCAR:", ValoresPorSku = new() { "AZ001", "AZ002", "AZ003", "AZ004", "AZ005", "AZ006" } },
        new() { DescripcionCampo = "FECHA DE ELABORACION DEL JARABE SIMPLE:", ValoresPorSku = new() { "10/10/2024", "09/10/2024", "11/10/2024", "12/10/2024", "13/10/2024", "14/10/2024" } },
        // ... repite para los demás campos visibles
        */}
            };
        }


        public static BloqueLotesPrincipalesModel ObtenerAnalisisSensorialJarabeSimple(string lote)
        {
            return new BloqueLotesPrincipalesModel
            {
                TituloBloque = "Resultados de Análisis Sensorial del Jarabe Simple",
                EncabezadosSku = new List<string> {
            "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "RESULTADO PANEL"
        },
                Registros = new List<RegistroLotesPrincipales>
        {
          /*  new() { DescripcionCampo = "LIBERADOR 1:", ValoresPorSku = new() { "María", "Luis", "Karla", "Pepe", "Sofía", "Laura", "Aprobado" } },
            new() { DescripcionCampo = "LIBERADOR 2:", ValoresPorSku = new() { "José", "Ana", "Miguel", "Elena", "Mario", "Beatriz", "Aprobado" } },
            new() { DescripcionCampo = "LOTE:", ValoresPorSku = new() { "L-001", "L-000", "L-002", "L-003", "L-004", "L-005", "L-001" } },
            new() { DescripcionCampo = "FECHA:", ValoresPorSku = new() { "15/10/2024", "14/10/2024", "16/10/2024", "17/10/2024", "18/10/2024", "19/10/2024", "15/10/2024" } },
            new() { DescripcionCampo = "REFERENCIA:", ValoresPorSku = new() { "REF-01", "REF-02", "REF-03", "REF-04", "REF-05", "REF-06", "REF-01" } },
        */}
            };
        }


        public static BloqueLotesPrincipalesModel ObtenerJarabeTerminado(
   List<TrazabilidadNode> trazabilidadNodos,
   long batchPadre,
   TimeSpan horaQueja, BatchInfo batchInfoJT)
        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            // 1. Obtener nodos de jarabe Terminado ordenados por hora
            var nodosJarabeTerminado = trazabilidadNodos
                .Where(n => n.ManufacturingFamily.ToUpper().Contains("F003")) // jarabe terminado
                .OrderBy(n => n.StartDate)
                .ToList();

            // 2. Buscar el que estaba activo a la hora de la queja
            var jarabeActivo = nodosJarabeTerminado
                .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

            

            // 3. Agregar activo + 4 anteriores
            var lotesSeleccionados = new List<TrazabilidadNode>();
            if (jarabeActivo != null)
            {
                lotesSeleccionados.Add(jarabeActivo);
                var anteriores = nodosJarabeTerminado
                    .Where(n => n.EndDate < jarabeActivo.StartDate)
                    .OrderByDescending(n => n.EndDate)
                    .Take(4)
                    .ToList();
                lotesSeleccionados.AddRange(anteriores);
            }
            else
            {
                lotesSeleccionados = nodosJarabeTerminado
                    .Where(n => n.EndDate < fechaHoraQueja)
                    .OrderByDescending(n => n.EndDate)
                    .Take(5)
                    .ToList();
            }

            // 4. Columnas: SKU QUEJA, ANTERIOR, OTRO INVOLUCRADO, ...
            int totalColumnas = Math.Max(2, lotesSeleccionados.Count);

            // Rellenar con nulls si hay menos de 2
            while (lotesSeleccionados.Count < totalColumnas)
            {
                lotesSeleccionados.Add(null);
            }

            var encabezados = new List<string>();
            for (int i = 0; i < totalColumnas; i++)
            {
                encabezados.Add(i == 0 ? "SKU QUEJA" : "ANTERIOR");
            }

            // 5. Inicializar registros por fila
            var campos = new List<string>
            {
                "LOTE DE JARABE TERMINADO:",
                "FECHA DE ELABORACIÓN:",
                "TANQUE:",
                "CLAVE:",
                "HORA INICIO DE LLENADO:",
                "HORA DE TERMINO LLENADO:",
                "VOLUMEN PREPARADO DE JARABE TERMINADO:",
                "VOLUMEN UTILIZADO DE JARABE TERMINADO:",
                
            };
            var registros = campos.Select(c => new RegistroLotesPrincipales
            {
                DescripcionCampo = c,
                ValoresPorSku = new List<string>()
            }).ToList();
            // 6. Llenar los valores por columna (por lote)
            foreach (var lote in lotesSeleccionados)
            {
                if (lote == null)
                {   // Agrega solo "" en todas las filas
                    foreach (var fila in registros)
                    {
                        fila.ValoresPorSku.Add("");
                    }
                    continue;
                }
                /*
                var hijos = trazabilidadNodos
                    .Where(x => x.BatchPadre == lote.Batch)
                    .ToList();

                var fructuosa = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("FRUCTUOSA"));
                var jarabesimple = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("JARABE SIMPLE"));
                var azucar = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("AZUCAR"));
                var aguaTratada = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("AGUA TRATADA"));
                // Buscar hasta 2 concentrados entre los hijos del lote actual
                var concentradosTop2 = hijos
                    .Where(h => !string.IsNullOrWhiteSpace(h.ManufacturingReferenceName)
                             && h.ManufacturingReferenceName.IndexOf("CONCENTRADO", StringComparison.OrdinalIgnoreCase) >= 0)
                    .GroupBy(h => h.Batch)                 // evita duplicados por batch (opcional)
                    .Select(g => g.First())
                    .OrderBy(h => h.StartDate)            // o .OrderBy(h => h.Batch) según te convenga
                    .Take(2)
                    .Select(h => h.BatchName ?? "")       // lo que quieras mostrar (BatchName / Batch / ManufacturingReferenceName)
                    .ToList();

                // Asegura 2 posiciones
                while (concentradosTop2.Count < 2) concentradosTop2.Add("");

               */

                registros[0].ValoresPorSku.Add(lote.BatchName ?? "");
                registros[1].ValoresPorSku.Add(lote.StartDate.ToString("dd/MM/yyyy HH:mm"));
                registros[2].ValoresPorSku.Add(lote.workplacename);
                registros[3].ValoresPorSku.Add("");
                registros[4].ValoresPorSku.Add(lote.StartDate.ToString("dd/MM/yyyy HH:mm") ?? "");
                registros[5].ValoresPorSku.Add(lote.EndDate.ToString("dd/MM/yyyy HH:mm") ?? "");
                registros[6].ValoresPorSku.Add(lote.bcquantity.ToString());
                registros[7].ValoresPorSku.Add(lote.consumedquantity.ToString());

                var hijos = trazabilidadNodos
               .Where(x => x.BatchPadre == lote.Batch)
               .ToList();

                foreach (var hijo in hijos)
                {
                    // Crea una fila nueva por cada hijo
                    var registroHijo = new RegistroLotesPrincipales
                    {
                        // puedes poner texto libre o el nombre del hijo
                        DescripcionCampo = "LOTE DE " + hijo.ManufacturingReferenceName ?? "",
                        ValoresPorSku = new List<string> { hijo.BatchIdentifier + " - " + hijo.BatchName ?? "" }
                    };

                    registros.Add(registroHijo);
                }
            }


            return new BloqueLotesPrincipalesModel
            {
                TituloBloque = "JARABE TERMINADO (NÚM. BATCH)",
                EncabezadosSku = encabezados,
                Registros = registros
            };
        }

        public static BloqueLotesPrincipalesModel ObtenerTratamientodeAgua(
   List<TrazabilidadNode> trazabilidadNodos,
   long batchPadre,
   TimeSpan horaQueja, BatchInfo batchInfoJT)
        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            var nodos = trazabilidadNodos
                .Where(n => n.ManufacturingFamily.ToUpper().Contains("F014")) // agua tratada
                .OrderBy(n => n.StartDate)
                .ToList();

            // 2. Buscar el que estaba activo a la hora de la queja
            var nodoActivo = nodos
                .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);


            // 3. Agregar activo + 4 anteriores
            var lotesSeleccionados = new List<TrazabilidadNode>();
            if (nodoActivo != null)
            {
                lotesSeleccionados.Add(nodoActivo);
                var anteriores = nodos
                    .Where(n => n.EndDate < nodoActivo.StartDate)
                    .OrderByDescending(n => n.EndDate)
                    .Take(4)
                    .ToList();
                lotesSeleccionados.AddRange(anteriores);
            }
            else
            {
                lotesSeleccionados = nodos
                    .Where(n => n.EndDate < fechaHoraQueja)
                    .OrderByDescending(n => n.EndDate)
                    .Take(5)
                    .ToList();
            }

            // 4. Columnas: SKU QUEJA, ANTERIOR, OTRO INVOLUCRADO, ...
            int totalColumnas = Math.Max(2, lotesSeleccionados.Count);

            // Rellenar con nulls si hay menos de 2
            while (lotesSeleccionados.Count < totalColumnas)
            {
                lotesSeleccionados.Add(null);
            }

            var encabezados = new List<string>();
            for (int i = 0; i < totalColumnas; i++)
            {
                encabezados.Add(i == 0 ? "SKU QUEJA" : "ANTERIOR");
            }
          
            // 5. Inicializar registros por fila
            var campos = new List<string>
            {
                "LOTE DE TRATAMIENTO DE AGUA:",               
            };
            var registros = campos.Select(c => new RegistroLotesPrincipales
            {
                DescripcionCampo = c,
                ValoresPorSku = new List<string>()
            }).ToList();
            // 6. Llenar los valores por columna (por lote)
            foreach (var lote in lotesSeleccionados)
            {
                if (lote == null)
                {   // Agrega solo "" en todas las filas
                    foreach (var fila in registros)
                    {
                        fila.ValoresPorSku.Add("");
                    }
                    continue;
                }               

                registros[0].ValoresPorSku.Add(lote.BatchName ?? "");

                var hijos = trazabilidadNodos
               .Where(x => x.BatchPadre == lote.Batch)
               .ToList();

                foreach (var hijo in hijos)
                {
                    // Crea una fila nueva por cada hijo
                    var registroHijo = new RegistroLotesPrincipales
                    {
                        // puedes poner texto libre o el nombre del hijo
                        DescripcionCampo = "LOTE DE " + hijo.ManufacturingReferenceName ?? "",
                        ValoresPorSku = new List<string> { hijo.BatchIdentifier + " - " + hijo.BatchName ?? "" }
                    };

                    registros.Add(registroHijo);
                }

            }
                

            return new BloqueLotesPrincipalesModel
            {
                TituloBloque = "TRATAMIENDO DE AGUA (NÚM. BATCH)",
                EncabezadosSku = encabezados,
                Registros = registros
            };
        }

        public static BloqueLotesPrincipalesModel ObtenerSaneo(
   List<TrazabilidadNode> trazabilidadNodos,
   long batchPadre,
   TimeSpan horaQueja, BatchInfo batchInfoJT)
        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            var nodos = trazabilidadNodos
                .Where(n => n.ManufacturingFamily.ToUpper().Contains("F015")) // saneos
                .OrderBy(n => n.StartDate)
                .ToList();

            // 2. Buscar el que estaba activo a la hora de la queja
            var nodoActivo = nodos
                .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);


            // 3. Agregar activo + 4 anteriores
            var lotesSeleccionados = new List<TrazabilidadNode>();
            if (nodoActivo != null)
            {
                lotesSeleccionados.Add(nodoActivo);
                var anteriores = nodos
                    .Where(n => n.EndDate < nodoActivo.StartDate)
                    .OrderByDescending(n => n.EndDate)
                    .Take(4)
                    .ToList();
                lotesSeleccionados.AddRange(anteriores);
            }
            else
            {
                lotesSeleccionados = nodos
                    .Where(n => n.EndDate < fechaHoraQueja)
                    .OrderByDescending(n => n.EndDate)
                    .Take(5)
                    .ToList();
            }

            // 4. Columnas: SKU QUEJA, ANTERIOR, OTRO INVOLUCRADO, ...
            int totalColumnas = Math.Max(2, lotesSeleccionados.Count);

            // Rellenar con nulls si hay menos de 2
            while (lotesSeleccionados.Count < totalColumnas)
            {
                lotesSeleccionados.Add(null);
            }

            var encabezados = new List<string>();
            for (int i = 0; i < totalColumnas; i++)
            {
                encabezados.Add(i == 0 ? "SKU QUEJA" : "ANTERIOR");
            }

            // 5. Inicializar registros por fila
            var campos = new List<string>
            {   
                "LOTE DE SANEO:",
               
            };
            var registros = campos.Select(c => new RegistroLotesPrincipales
            {
                DescripcionCampo = c,
                ValoresPorSku = new List<string>()
            }).ToList();
            // 6. Llenar los valores por columna (por lote)
            foreach (var lote in lotesSeleccionados)
            {
                if (lote == null)
                {   // Agrega solo "" en todas las filas
                    foreach (var fila in registros)
                    {
                        fila.ValoresPorSku.Add("");
                    }
                    continue;
                }
             
                registros[0].ValoresPorSku.Add(lote.BatchName ?? "");
                var hijos = trazabilidadNodos
                 .Where(x => x.BatchPadre == lote.Batch)
                 .ToList();

                foreach (var hijo in hijos)
                {
                    // Crea una fila nueva por cada hijo
                    var registroHijo = new RegistroLotesPrincipales
                    {
                        // puedes poner texto libre o el nombre del hijo
                        DescripcionCampo = "LOTE DE " + hijo.ManufacturingReferenceName ?? "",
                        ValoresPorSku = new List<string> { hijo.BatchIdentifier + " - " + hijo.BatchName ?? "" }
                    };

                    registros.Add(registroHijo);
                }
            }


            return new BloqueLotesPrincipalesModel
            {
                TituloBloque = "TRATAMIENDO DE AGUA (NÚM. BATCH)",
                EncabezadosSku = encabezados,
                Registros = registros
            };
        }
        

        public static BloqueLotesPrincipalesModel ObtenerAnalisisSensorialJarabeTerminado(string lote)
        {
            return new BloqueLotesPrincipalesModel
            {
                TituloBloque = "Resultados de Análisis Sensorial del Jarabe Terminado",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "RESULTADO PANEL" },
                Registros = new List<RegistroLotesPrincipales>
        {
           /* new() { DescripcionCampo = "LIBERADOR 1:", ValoresPorSku = new() { "Juan P.", "Luis G.", "Ana R.", "Carlos S.", "Marta C.", "✅" } },
            new() { DescripcionCampo = "LIBERADOR 2:", ValoresPorSku = new() { "Laura D.", "Beatriz Z.", "Ricardo M.", "Silvia V.", "Alejandro B.", "✅" } },
            new() { DescripcionCampo = "LOTE:", ValoresPorSku = new() { "JT001", "JT002", "JT003", "JT004", "JT005", "-" } },
            new() { DescripcionCampo = "FECHA:", ValoresPorSku = new() { "15/10/2024", "14/10/2024", "13/10/2024", "12/10/2024", "11/10/2024", "-" } },
            new() { DescripcionCampo = "REFERENCIA:", ValoresPorSku = new() { "REF-101", "REF-102", "REF-103", "REF-104", "REF-105", "-" } }
        */}
            };
        }

        public static List<PruebaLiberacionModel> ObtenerPruebasLiberacion(string lote)
        {
            // Aquí simulas los datos. Después puedes cambiarlos por una consulta a DB.
            var lista = new List<PruebaLiberacionModel>
    {
                /*   new PruebaLiberacionModel { Parametro = "BRIX FRESCO:", InicioCorrida = "NA", MedioCorrida = "NA", FinCorrida = "NA" },
                   new PruebaLiberacionModel { Parametro = "BRIX/CONCENTRACIÓN:", InicioCorrida = "12.43", MedioCorrida = "12.38", FinCorrida = "12.38" },
                   new PruebaLiberacionModel { Parametro = "GAS:", InicioCorrida = "3.71", MedioCorrida = "3.64", FinCorrida = "3.68" },
                   new PruebaLiberacionModel { Parametro = "APARIENCIA:", InicioCorrida = "SIN APARIENCIA ANOMALA", MedioCorrida = "SIN APARIENCIA ANOMALA", FinCorrida = "SIN APARIENCIA ANOMALA" },
                   new PruebaLiberacionModel { Parametro = "CONTENIDO NETO:", InicioCorrida = "1511.08", MedioCorrida = "1507.32", FinCorrida = "1499.82" },
                   new PruebaLiberacionModel { Parametro = "TORQUE:", InicioCorrida = "10.23", MedioCorrida = "12.42", FinCorrida = "12.02" },
                   new PruebaLiberacionModel { Parametro = "ACIDEZ:", InicioCorrida = "NA", MedioCorrida = "NA", FinCorrida = "NA" },
                   new PruebaLiberacionModel { Parametro = "PH:", InicioCorrida = "NA", MedioCorrida = "NA", FinCorrida = "NA" },
                   new PruebaLiberacionModel { Parametro = "PRUEBA DE FUGA:", InicioCorrida = "OK", MedioCorrida = "OK", FinCorrida = "OK" },
                   new PruebaLiberacionModel { Parametro = "CÓDIGO LEGIBLE:", InicioCorrida = "OK", MedioCorrida = "OK", FinCorrida = "OK" },
                   new PruebaLiberacionModel { Parametro = "OLOR:", InicioCorrida = "SIN OLOR ANOMALO", MedioCorrida = "SIN OLOR ANOMALO", FinCorrida = "SIN OLOR ANOMALO" },
                   new PruebaLiberacionModel { Parametro = "SABOR:", InicioCorrida = "SIN SABOR ANOMALO", MedioCorrida = "SIN SABOR ANOMALO", FinCorrida = "SIN SABOR ANOMALO" },
                   new PruebaLiberacionModel { Parametro = "LOTE DE AGUA TRATADA:", InicioCorrida = "10241843", MedioCorrida = "10241843", FinCorrida = "10246563" },
                   new PruebaLiberacionModel { Parametro = "ENJUAGUE FINAL - LOTE DE AGUA:", InicioCorrida = "15/10/2024", MedioCorrida = "15/10/2024", FinCorrida = "15/10/2024" },
               */
            };

            return lista;
        }

        public static List<LiberacionSensorialProductoModel> ObtenerLiberacionSensorialProducto(string lote)
        {
            // Aquí debes conectarte a tu origen real de datos (ej. base de datos o API)
            // A modo de ejemplo, te lo dejo simulado:
            return new List<LiberacionSensorialProductoModel>
    {
       /* new() {
            Panelista = "Hector Meraz/ Marcos Rogel/Jonathan Mata",
            FechaLiberacion = "16-oct-24",
            Producto = "Manzana 1.5",
            CodigoMTRA = "29DIC24-3 20:44",
            Referencia = "14DIC24-3 10:31",
            ResultadoPanel = "IN-PASA"
        },
        new() {
            Panelista = "Hector Meraz/ Marcos Rogel/Jonathan Mata",
            FechaLiberacion = "16-oct-24",
            Producto = "Manzana 1.5",
            CodigoMTRA = "30DIC24-3 00:35",
            Referencia = "14DIC24-3 10:31",
            ResultadoPanel = "IN-PASA"
        },
        // ...otros registros
    */};
        }


        public static class DynamicSqlService
        {
            public static List<TModel> EjecutarQuery<TModel>(
                string bloque,
                Dictionary<string, object> parametros,
                Dictionary<long, string> descripciones,
                IConfiguration configuration,
                string connectionString
            ) where TModel : new()
            {
                var config = configuration.GetSection($"ExcelToSqlMappings:{bloque}")
                                          .Get<SqlMappingConfig>();

                var resultados = new List<TModel>();
                var encontrados = new HashSet<long>();

                // Reemplazar placeholders en el query
                string query = config.Query;
                foreach (var param in parametros)
                {
                    if (param.Key.StartsWith("{")) // Reemplazo directo (como {lotes})
                        query = query.Replace(param.Key, param.Value.ToString());
                }

                using var conn = new SqlConnection(connectionString);
                using var cmd = new SqlCommand(query, conn);

                // Solo parámetros con @ se agregan a SqlCommand
                foreach (var param in parametros.Where(p => p.Key.StartsWith("@")))
                    cmd.Parameters.AddWithValue(param.Key, param.Value);

                conn.Open();
                using var reader = cmd.ExecuteReader();

                var props = typeof(TModel).GetProperties();

                while (reader.Read())
                {
                    var modelo = new TModel();

                    foreach (var map in config.ColumnMappings)
                    {
                        var prop = props.FirstOrDefault(p => p.Name == map.Key);
                        if (prop != null && reader[map.Value] != DBNull.Value)
                        {
                            var value = reader[map.Value];
                            if (prop.PropertyType == typeof(string))
                                value = value.ToString();

                            prop.SetValue(modelo, value);
                        }
                    }

                    // Añadir Descripcion desde diccionario
                    if (props.Any(p => p.Name == "Descripcion"))
                    {
                        var id = Convert.ToInt64(reader[config.DescripcionField]);
                        encontrados.Add(id);

                        var propDesc = props.First(p => p.Name == "Descripcion");
                        string descripcion = descripciones.ContainsKey(id) ? descripciones[id] : "Sin descripción";
                        propDesc.SetValue(modelo, descripcion);
                    }

                    resultados.Add(modelo);
                }
                if (descripciones != null)
                {
                    // Agregar los no encontrados
                    foreach (var kvp in descripciones)
                    {
                        if (!encontrados.Contains(kvp.Key))
                        {
                            var modelo = new TModel();
                            var propDesc = props.FirstOrDefault(p => p.Name == "Descripcion");
                            propDesc?.SetValue(modelo, kvp.Value);

                            foreach (var prop in props)
                            {
                                if (prop.Name != "Descripcion")
                                    prop.SetValue(modelo, "");
                            }

                            resultados.Add(modelo);
                        }
                    }
                }
                return resultados;
            }


            public static string EjecutarEscalar(
   string sql,
   Dictionary<string, object> parametros,
   string connectionString)
            {
                using var conn = new SqlConnection(connectionString);
                using var cmd = new SqlCommand(sql, conn);

                foreach (var p in parametros)
                    cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);

                conn.Open();
                var result = cmd.ExecuteScalar();
                return result?.ToString();
            }

        }


        public static (BloqueLotesPrincipalesModel Modelo, TrazabilidadNode? JarabeActivo) ObtenerJarabeSimpleConContacto(
        List<TrazabilidadNode> trazabilidadNodos,
        long batchPadre,
        TimeSpan horaQueja)

        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            // 1. Obtener nodos de jarabe simple ordenados por hora
            var nodosJarabeSimple = trazabilidadNodos
                .Where(n => n.ManufacturingFamily.ToUpper().Contains("F002")) // jarabe simple
                .OrderBy(n => n.StartDate)
                .ToList();

            // 2. Buscar el que estaba activo a la hora de la queja
            var jarabeActivo = nodosJarabeSimple
                .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

            // 3. Agregar activo + 4 anteriores
            var lotesSeleccionados = new List<TrazabilidadNode>();
            if (jarabeActivo != null)
            {
                lotesSeleccionados.Add(jarabeActivo);
                var anteriores = nodosJarabeSimple
                    .Where(n => n.EndDate < jarabeActivo.StartDate)
                    .OrderByDescending(n => n.EndDate)
                    .Take(4)
                    .ToList();
                lotesSeleccionados.AddRange(anteriores);
            }
            else
            {
                lotesSeleccionados = nodosJarabeSimple
                    .Where(n => n.EndDate < fechaHoraQueja)
                    .OrderByDescending(n => n.EndDate)
                    .Take(5)
                    .ToList();
            }

            // 4. Columnas: SKU QUEJA, ANTERIOR, OTRO INVOLUCRADO, ...
            int totalColumnas = Math.Max(2, lotesSeleccionados.Count);

            // Rellenar con nulls si hay menos de 2
            while (lotesSeleccionados.Count < totalColumnas)
            {
                lotesSeleccionados.Add(null);
            }

            var encabezados = new List<string>();
            for (int i = 0; i < totalColumnas; i++)
            {
                encabezados.Add(i == 0 ? "SKU QUEJA" : "ANTERIOR");
            }

            // 5. Inicializar registros por fila
            var campos = new List<string>
    {
        "LOTE DE JARABE SIMPLE (NÚM. BATCH):",
        "# TANQUE DONDE SE ALMACENÓ EL JARABE SIMPLE:",
        "FECHA DE ELABORACIÓN DEL JARABE SIMPLE:",
        "VOLUMEN PREPARADO DE JARABE SIMPLE:",
        "VOLUMEN UTILIZADO DE JARABE SIMPLE:",
        "# TANQUE DISOLUTOR DONDE SE PREPARÓ EL JARABE SIMPLE:",
       
    };
         

            var registros = campos.Select(c => new RegistroLotesPrincipales
            {
                DescripcionCampo = c,
                ValoresPorSku = new List<string>()
            }).ToList();

            // 6. Llenar los valores por columna (por lote)
            foreach (var lote in lotesSeleccionados)
            {
                if (lote == null)
                {
                    // Agrega solo "" en todas las filas
                    foreach (var fila in registros)
                    {
                        fila.ValoresPorSku.Add("");
                    }
                    continue;
                }
             
                registros[0].ValoresPorSku.Add(lote.BatchName ?? "");
                registros[1].ValoresPorSku.Add(lote.workplacename);                
                registros[2].ValoresPorSku.Add(lote.StartDate.ToString("dd/MM/yyyy HH:mm"));
                registros[3].ValoresPorSku.Add(lote.bcquantity.ToString());
                registros[4].ValoresPorSku.Add(lote.consumedquantity.ToString());
                registros[5].ValoresPorSku.Add(""); // falta traer un dato extra de lote,

                var hijos = trazabilidadNodos
                .Where(x => x.BatchPadre == lote.Batch)
                .ToList();

                foreach (var hijo in hijos)
                {
                    // Crea una fila nueva por cada hijo
                    var registroHijo = new RegistroLotesPrincipales
                    {
                        // puedes poner texto libre o el nombre del hijo
                        DescripcionCampo = "LOTE DE " + hijo.ManufacturingReferenceName ?? "",
                        ValoresPorSku = new List<string> { hijo.BatchIdentifier + " - " + hijo.BatchName ?? "" }
                    };

                    registros.Add(registroHijo);
                }
            }
           

            var modelo = new BloqueLotesPrincipalesModel
            {
                TituloBloque = "LOTE DE JARABE SIMPLE (NÚM. BATCH). MENCIONAR LOTE CORRESPONDIENTE A LA MUESTRA Y 2 ANTERIORES QUE PUDIERON HABER ESTADO EN CONTACTO",
                EncabezadosSku = encabezados,
                Registros = registros
            };

            // Devuelve la tupla (modelo, jarabeActivo)
            return (modelo, jarabeActivo);

        }

        private static TrazabilidadNode BuscarHijoPorPalabra(List<TrazabilidadNode> hijos, string palabra)
        {
            return hijos.FirstOrDefault(h => h.ManufacturingReferenceName != null && h.ManufacturingReferenceName.ToUpper().Contains(palabra));
        }



        public static BloqueAnalisisFisicoquimicoModel ObtenerAnalisisFisicoquimico(
    List<TrazabilidadNode> trazabilidadNodos,
    long batchPadre,
    TimeSpan horaQueja,
    string company,
    string connStr,
    IConfiguration _configuration, string bloque, string titulobloque, string query) // 👈 aquí

        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

         
            var nodosJarabeSimple = trazabilidadNodos
                .Where(n => n.ManufacturingFamily.ToUpper().Contains(bloque)) 
                .OrderBy(n => n.StartDate)
                .ToList();
            if (nodosJarabeSimple.Count() > 0)
            {


                var jarabeActivo = nodosJarabeSimple
                    .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

                var lotesSeleccionados = new List<TrazabilidadNode>();
                if (jarabeActivo != null)
                {
                    lotesSeleccionados.Add(jarabeActivo);
                    var anteriores = nodosJarabeSimple
                        .Where(n => n.EndDate < jarabeActivo.StartDate)
                        .OrderByDescending(n => n.EndDate)
                        .Take(4)
                        .ToList();
                    lotesSeleccionados.AddRange(anteriores);
                }
                else
                {
                    lotesSeleccionados = nodosJarabeSimple;
                }

                int totalColumnas = Math.Max(2, lotesSeleccionados.Count);
                while (lotesSeleccionados.Count < totalColumnas)
                    lotesSeleccionados.Add(null);

                var encabezados = new List<string>();
                for (int i = 0; i < totalColumnas; i++)
                    encabezados.Add(i == 0 ? "SKU QUEJA" : "ANTERIOR");

                // 2. Ejecutar query SQL
                var parametros = new Dictionary<string, object>
            {
                { "@company", company },
                { "{lotes}", string.Join(",", lotesSeleccionados.Where(l => l != null).Select(l => l.Batch)) }
            };

                var resultados = DynamicSqlService.EjecutarQuery<ResultadoAnalisisFisicoquimico>(
                    query,
                parametros,
                    null,
                    _configuration,
                    connStr
                );

                // 3. Obtener lista única de parámetros (campos verticales)
                var nombresParametros = resultados
                .Select(r => r.OperacionNombre)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(Norm)                 // <- normalizamos
                .Distinct()
                .OrderBy(n => n)
                .ToList();

                var registros = nombresParametros
                    .Select(nombre => new RegistroAnalisisFisicoquimico
                    {
                        DescripcionParametro = nombre,  // ya normalizado
                        ValoresPorLote = new List<string>()
                    })
                    .ToList();

                // 3.1 Índice por (Lote, Operación normalizada) -> último resultado
                //    Si tienes un campo de fecha en ResultadoAnalisisFisicoquimico (usa el correcto abajo)
                var index = resultados
                  .Where(r => ToLong(r.Lote).HasValue)
                  .GroupBy(r => (Lote: ToLong(r.Lote).Value, Op: Norm(r.OperacionNombre)))
                  .ToDictionary(
                      g => g.Key,
                      g => g.First()   // simplemente el primero
                  );


                // 4. Llenar columnas
                // 4. Llenar columnas
                foreach (var lote in lotesSeleccionados)
                {
                    foreach (var registro in registros)
                    {
                        if (lote == null)
                        {
                            registro.ValoresPorLote.Add("");
                            continue;
                        }

                        var key = (Lote: lote.Batch, Op: registro.DescripcionParametro); // ambos ya homogeneizados
                        index.TryGetValue(key, out var resultado);

                        if (resultado == null)
                        {
                            registro.ValoresPorLote.Add("");
                            continue;
                        }

                        string valorFinal;
                        if (resultado.TipoOperacion == 1)
                        {
                            valorFinal = string.IsNullOrWhiteSpace(resultado.Valor) ? "" : resultado.Valor;
                        }
                        else if (resultado.TipoOperacion == 2)
                        {
                            valorFinal = resultado.Atributo == "1" ? "✔"
                                       : resultado.Atributo == "0" ? "✘"
                                       : "";
                        }
                        else
                        {
                            valorFinal = "";
                        }

                        registro.ValoresPorLote.Add(valorFinal);
                    }
                }

                return new BloqueAnalisisFisicoquimicoModel
                {
                    TituloBloque = titulobloque,
                    EncabezadosSku = encabezados,
                    Registros = registros
                };
            } else
            {
                return new BloqueAnalisisFisicoquimicoModel
                {
                    TituloBloque = titulobloque,
                    EncabezadosSku = null,
                    Registros = null
                };
            }
        }



        public static BloqueAnalisisFisicoquimicoModel ObtenerAnalisisFisicoquimicoBydate(
    List<TrazabilidadNode> trazabilidadNodos,
    long batchPadre,
    TimeSpan horaQueja,
    string company,
    string connStr,
    IConfiguration configuration,
    string bloque,
    string tituloBloque,
    string queryKey = "AnalisisFisicoquimicoBydate")
        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            var nodosFamilia = trazabilidadNodos
                .Where(n => (n.ManufacturingFamily ?? "").ToUpper().Contains(bloque?.ToUpper() ?? ""))
                .OrderBy(n => n.StartDate)
                .ToList();

            if (nodosFamilia.Count == 0)
                return new BloqueAnalisisFisicoquimicoModel { TituloBloque = tituloBloque };

            // 1) Lote activo y anteriores
            var jarabeActivo = nodosFamilia
                .FirstOrDefault(n => fechaHoraQueja >= n.StartDate && fechaHoraQueja <= n.EndDate);

            var seleccion = new List<TrazabilidadNode>();
            if (jarabeActivo != null)
            {
                seleccion.Add(jarabeActivo);
                seleccion.AddRange(
                    nodosFamilia.Where(n => n.EndDate < jarabeActivo.StartDate)
                                .OrderByDescending(n => n.EndDate)
                                .Take(4));
            }
            else
            {
                seleccion = nodosFamilia;
            }

            // 2) Ventana de fechas para el query por fecha (primera a última muestra)
            var startDate = seleccion.Min(n => n.StartDate);
            var endDate = seleccion.Max(n => n.EndDate);

            // 3) Manufactura / Workplace: primero del activo, si no, del padre (o del primer nodo de la selección)
            string manufacturingReference =
                jarabeActivo?.ManufacturingReference
                ?? nodoPadre?.ManufacturingReference
                ?? seleccion.FirstOrDefault()?.ManufacturingReference;

          

            // 4) Ejecutar el query mapeado en appsettings ("AnalisisFisicoquimicoBydate")
            var query = configuration[$"ExcelToSqlMappings:{queryKey}:Query"];
            // Buscar el workplace real desde la BD usando el batch del jarabe activo
            string workplace = null;
            if (jarabeActivo != null)
            {
                string sqlWorkplace = "SELECT TOP 1 consumptionWorkplace FROM BatchConsumptions WHERE batch = @batch";
                var pWork = new Dictionary<string, object> { { "@batch", jarabeActivo.Batch } };
                workplace = DynamicSqlService.EjecutarEscalar(sqlWorkplace, pWork, connStr);
            }
            // Validaciones mínimas
            if (string.IsNullOrWhiteSpace(manufacturingReference) || string.IsNullOrWhiteSpace(workplace))
                return new BloqueAnalisisFisicoquimicoModel { TituloBloque = tituloBloque };



            var parametros = new Dictionary<string, object>
    {
        { "@company", company },
        { "@startdate", startDate },
        { "@enddate",   endDate },
        { "@manufacturingreference", manufacturingReference },
        { "@workplace", workplace }
    };


            var filas = DynamicSqlService.EjecutarQuery<ResultadoAnalisisFisicoquimicoByDate>(
    queryKey, parametros, null, configuration, connStr);

            // 5) Render: tres columnas fijas (inicio/medio/fin) ya calculadas en SQL
            var encabezados = new List<string> { "inicio de corrida", "medio corrida", "fin de corrida" };

            var registros = filas
                .OrderBy(f => f.OperacionNombre)
                .Select(f => new RegistroAnalisisFisicoquimico
                {
                    DescripcionParametro = f.OperacionNombre,
                    ValoresPorLote = new List<string>
                    {
                f.InicioValor ?? "",
                f.MedioValor  ?? "",
                f.FinValor    ?? ""
                    }
                })
                .ToList();

            return new BloqueAnalisisFisicoquimicoModel
            {
                TituloBloque = tituloBloque,
                EncabezadosSku = encabezados,
                Registros = registros
            };
        }





        public static async Task<List<PruebaLiberacionRow>> ObtenerPruebasLiberacionJarabeTerminadoAsync(
    DateTime? startDate,
    DateTime? endDate,
    string workplace,
    string company,
    string manufacturingReference,
    string connectionString)
        {
            var result = new List<PruebaLiberacionRow>();

            if (startDate == null || endDate == null) return result;

            var middleDate = startDate.Value.AddSeconds((endDate.Value - startDate.Value).TotalSeconds / 2);

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                // 1. Obtener lista de operaciones únicas
                var operaciones = await connection.QueryAsync<(string ControlOperation, string ControlOperationName)>(@"
    SELECT DISTINCT CPO.controlOperation, CPO.controlOperationName
    FROM ControlProcedureResult CPR
    INNER JOIN ControlProcedureOperation CPO
        ON CPR.company = CPO.company AND CPR.controlProcedure = CPO.controlProcedure
    INNER JOIN CProcResultWithValuesStatus CPrvs
        ON CPrvs.company = CPR.company AND CPrvs.idControlProcedureResult = CPR.idControlProcedureResult and CPO.controlOperation = CPrvs.controlOperation
    WHERE CPrvs.launchingDate BETWEEN @StartDate AND @EndDate
      AND CPR.company = @Company
      AND CPrvs.workplace = @Workplace
      AND CPR.manufacturingOrder = @Reference
      AND CPrvs.resultValue IS NOT NULL",
     new
     {
         StartDate = startDate,
         EndDate = endDate,
         Workplace = workplace,
         Company = company,
         Reference = manufacturingReference
     });


                foreach (var operacion in operaciones)
                {
                    var inicio = await connection.QueryFirstOrDefaultAsync<string>(@"
                SELECT TOP 1 CPrvs.resultValue
                FROM ControlProcedureResult CPR
                INNER JOIN ControlProcedureOperation CPO
                    ON CPR.company = CPO.company AND CPR.controlProcedure = CPO.controlProcedure
                INNER JOIN CProcResultWithValuesStatus CPrvs
                    ON CPrvs.company = CPR.company AND CPrvs.idControlProcedureResult = CPR.idControlProcedureResult and CPO.controlOperation = CPrvs.controlOperation
                WHERE CPR.company = @Company
                  AND CPrvs.workplace = @Workplace
                  AND CPR.manufacturingOrder = @Reference
                  AND CPO.controlOperation = @OperacionId
                  AND CPrvs.launchingDate BETWEEN @StartDate AND @EndDate
                ORDER BY CPrvs.launchingDate ASC",
                        new
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            Workplace = workplace,
                            Company = company,
                            Reference = manufacturingReference,
                            OperacionId = operacion.ControlOperation
                        });

                    var medio = await connection.QueryFirstOrDefaultAsync<string>(@"
                SELECT TOP 1 CPrvs.resultValue
                FROM ControlProcedureResult CPR
                INNER JOIN ControlProcedureOperation CPO
                    ON CPR.company = CPO.company AND CPR.controlProcedure = CPO.controlProcedure
                INNER JOIN CProcResultWithValuesStatus CPrvs
                    ON CPrvs.company = CPR.company AND CPrvs.idControlProcedureResult = CPR.idControlProcedureResult and CPO.controlOperation = CPrvs.controlOperation
                WHERE CPR.company = @Company
                  AND CPrvs.workplace = @Workplace
                  AND CPR.manufacturingOrder = @Reference
                  AND CPO.controlOperation = @OperacionId
                  AND CPrvs.launchingDate BETWEEN @StartDate AND @EndDate
                ORDER BY ABS(DATEDIFF(SECOND, CPrvs.launchingDate, @MiddleDate))",
                        new
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            Workplace = workplace,
                            Company = company,
                            Reference = manufacturingReference,
                            OperacionId = operacion.ControlOperation,
                            MiddleDate = middleDate
                        });

                    var fin = await connection.QueryFirstOrDefaultAsync<string>(@"
                SELECT TOP 1 CPrvs.resultValue
                FROM ControlProcedureResult CPR
                INNER JOIN ControlProcedureOperation CPO
                    ON CPR.company = CPO.company AND CPR.controlProcedure = CPO.controlProcedure
                INNER JOIN CProcResultWithValuesStatus CPrvs
                    ON CPrvs.company = CPR.company AND CPrvs.idControlProcedureResult = CPR.idControlProcedureResult and CPO.controlOperation = CPrvs.controlOperation
                WHERE CPR.company = @Company
                  AND CPrvs.workplace = @Workplace
                  AND CPR.manufacturingOrder = @Reference
                  AND CPO.controlOperation = @OperacionId
                  AND CPrvs.launchingDate BETWEEN @StartDate AND @EndDate
                ORDER BY CPrvs.launchingDate DESC",
                        new
                        {
                            StartDate = startDate,
                            EndDate = endDate,
                            Workplace = workplace,
                            Company = company,
                            Reference = manufacturingReference,
                            OperacionId = operacion.ControlOperation
                        });

                    result.Add(new PruebaLiberacionRow
                    {
                        ControlOperationName = operacion.ControlOperationName,
                        InicioCorrida = inicio,
                        MedioCorrida = medio,
                        FinCorrida = fin
                    });
                }
            }

            return result;
        }

        // ---- WS -----

        public static async Task<result_Q_MateriaPrima> getBloqueMateriaPrima(string token, string url, string company, string trazalog, string query, string lotes)
        {

            HttpClient client = Method_Headers(token, url);
            var jsonBody = "{ 'COMP': '" + company + "', 'LOTES': '" + lotes + "' }";
            return await WebServiceHelper.SafePostAndDeserialize<result_Q_MateriaPrima>(
                client,
                client.BaseAddress.ToString(),
                jsonBody,
                query,
                trazalog
            );
        }

        // ------------ 

        // --- helpers ---

       

        static string Norm(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Trim();
            // quitar acentos
            var nf = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(nf.Length);
            foreach (var ch in nf)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }
        static long? ToLong(object v)
        {
            if (v == null) return null;
            if (v is long l) return l;
            if (v is int i) return i;
            if (long.TryParse(v.ToString(), out var p)) return p;
            return null;
        }

    }
}
