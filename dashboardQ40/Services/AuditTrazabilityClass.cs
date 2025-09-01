using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System.Data.SqlClient;
using System.Globalization;
using System.Text;
using static dashboardQ40.Models.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace dashboardQ40.Services
{
    public class AuditTrazabilityClass
    {
        /*
        public static BloqueMateriaPrimaModel ObtenerDatosMateriaPrima(string lote)
        {
            return new BloqueMateriaPrimaModel
            {
                Registros = new List<RegistroMateriaPrima>
                {
                    new RegistroMateriaPrima
                    {
                        Descripcion = "FRUCTOSA",
                        Proveedor = "Almex",
                        LoteExterno = "MI10132491 / MI10132956",
                        LoteInterno = "RSH41024-901 / RSH411024-901",
                        FechaRecepcion = "14-oct-24 / 14-oct-24",
                        Cantidad = "8,706 Kg / 10,740 Kg"
                    },
                    new RegistroMateriaPrima
                    {
                        Descripcion = "CO2",
                        Proveedor = "Linde",
                        LoteExterno = "79781247",
                        LoteInterno = "CD241024-710",
                        FechaRecepcion = "16/oct/24",
                        Cantidad = "1,064.211 Kg"
                    },
                    // agrega más según tu tabla...
                },
                SupervisorCalidad = "Margarita Antonio",
                SupervisorAlmacen = "Raul Juarez"
            };
        }
        */

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

                                Proveedor = reader["supplier"]?.ToString() ?? "N/A",
                                LoteExterno = reader["supplierBatch"]?.ToString() ?? "N/A",

                                // LoteInterno usa el BatchIdentifier
                                LoteInterno = descripciones.ContainsKey(loteId)
        ? descripciones[loteId].BatchName
        : loteId.ToString(), // fallback si no está
                                FechaRecepcion = reader["issueDate"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["issueDate"]).ToString("dd/MM/yyyy")
                                    : "N/A",
                                Cantidad = reader["realQuantityInParcel"]?.ToString() ?? "N/A"
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
                        Proveedor = "N/A",
                        LoteExterno = "N/A",
                        LoteInterno = kvp.Value.BatchName, // usa BatchIdentifier aquí
                        FechaRecepcion = "N/A",
                        Cantidad = "N/A"
                    });
                }
            }

            return resultados;
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

        public static BloqueJarabeSimpleModel ObtenerJarabeSimplePorLote(string lote)
        {
            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "Lote de Jarabe Simple (Núm. Batch)",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO" },
                Registros = new List<RegistroJarabeSimple>
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


        public static BloqueJarabeSimpleModel ObtenerJarabeSimpleConContacto(string lote)
        {
            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "Lote de Jarabe Simple (Núm. Batch). Mencionar lote correspondiente a la muestra y 2 anteriores que pudieron haber estado en contacto",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO" },

                Registros = new List<RegistroJarabeSimple>
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


        public static BloqueJarabeSimpleModel ObtenerAnalisisSensorialJarabeSimple(string lote)
        {
            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "Resultados de Análisis Sensorial del Jarabe Simple",
                EncabezadosSku = new List<string> {
            "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "RESULTADO PANEL"
        },
                Registros = new List<RegistroJarabeSimple>
        {
          /*  new() { DescripcionCampo = "LIBERADOR 1:", ValoresPorSku = new() { "María", "Luis", "Karla", "Pepe", "Sofía", "Laura", "Aprobado" } },
            new() { DescripcionCampo = "LIBERADOR 2:", ValoresPorSku = new() { "José", "Ana", "Miguel", "Elena", "Mario", "Beatriz", "Aprobado" } },
            new() { DescripcionCampo = "LOTE:", ValoresPorSku = new() { "L-001", "L-000", "L-002", "L-003", "L-004", "L-005", "L-001" } },
            new() { DescripcionCampo = "FECHA:", ValoresPorSku = new() { "15/10/2024", "14/10/2024", "16/10/2024", "17/10/2024", "18/10/2024", "19/10/2024", "15/10/2024" } },
            new() { DescripcionCampo = "REFERENCIA:", ValoresPorSku = new() { "REF-01", "REF-02", "REF-03", "REF-04", "REF-05", "REF-06", "REF-01" } },
        */}
            };
        }


        public static BloqueJarabeSimpleModel ObtenerJarabeTerminado(
   List<TrazabilidadNode> trazabilidadNodos,
   long batchPadre,
   TimeSpan horaQueja, BatchInfo batchInfoJT)
        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            // 1. Obtener nodos de jarabe Terminado ordenados por hora
            var nodosJarabeTerminado = trazabilidadNodos
                .Where(n => n.ManufacturingReferenceName.ToUpper().Contains("JARABE TERM"))
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
                "LOTE DE FRUCTOSA:",
                "LOTE DE AZUCAR:",
                "NÚMERO DE BATCH",
                "LOTE DE AGUA TRATADA:",
                "LOTE DE CONCENTRADOS PARTE 1:",
                "LOTE DE CONCENTRADOS PARTE 2:",
                "PARTES LIQUIDAS Y CONSECUTIVAS DE BOLSAS:",
                "PARTES SECAS Y CONSECUTIVAS DE BOLSAS:"
            };
            var registros = campos.Select(c => new RegistroJarabeSimple
            {
                DescripcionCampo = c,
                ValoresPorSku = new List<string>()
            }).ToList();
            // 6. Llenar los valores por columna (por lote)
            foreach (var lote in lotesSeleccionados)
            {
                if (lote == null)
                {   // Agrega solo "N/A" en todas las filas
                    foreach (var fila in registros)
                    {
                        fila.ValoresPorSku.Add("N/A");
                    }
                    continue;
                }
                var hijos = trazabilidadNodos
                    .Where(x => x.BatchPadre == lote.Batch)
                    .ToList();

                var fructuosa = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("FRUCTUOSA"));
                var jarabesimple = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("JARABE SIMPLE"));
                var azucar = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("AZUCAR"));
                var aguaTratada = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("AGUA TRATADA"));
                

                registros[0].ValoresPorSku.Add(lote.BatchName ?? "N/A");
                registros[1].ValoresPorSku.Add(lote.StartDate.ToString("dd/MM/yyyy HH:mm"));
                registros[2].ValoresPorSku.Add(batchInfoJT.workplace ?? "N/A");
                registros[3].ValoresPorSku.Add("N/A");
                registros[4].ValoresPorSku.Add(batchInfoJT.StartDate?.ToString("dd/MM/yyyy HH:mm") ?? "N/A");
                registros[5].ValoresPorSku.Add(batchInfoJT.EndDate?.ToString("dd/MM/yyyy HH:mm") ?? "N/A");
                registros[6].ValoresPorSku.Add(batchInfoJT.initialQuantity.ToString() ?? "N/A");
                registros[7].ValoresPorSku.Add("N/A");
                registros[8].ValoresPorSku.Add("N/A");       
                registros[9].ValoresPorSku.Add(fructuosa?.BatchName.ToString() ?? "N/A"); //Fructuosa
                registros[10].ValoresPorSku.Add(azucar?.BatchName.ToString() ?? "N/A");      // azucar

                registros[11].ValoresPorSku.Add(jarabesimple?.BatchName.ToString() ?? "N/A"); //jarabeActivo simple
                registros[12].ValoresPorSku.Add(aguaTratada?.BatchName.ToString() ?? "N/A");
                registros[13].ValoresPorSku.Add("N/A");
                registros[14].ValoresPorSku.Add("N/A");
                registros[15].ValoresPorSku.Add("N/A");
            }


            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "JARABE TERMINADO (NÚM. BATCH)",
                EncabezadosSku = encabezados,
                Registros = registros
            };
        }
        /*
        public static BloqueJarabeSimpleModel ObtenerJarabeTerminado(string lote)
        {
            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "Jarabe Terminado (Núm. Batch)",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO" },
                Registros = new List<RegistroJarabeSimple>
        {
            new() { DescripcionCampo = "LOTE DE JARABE TERMINADO:", ValoresPorSku = new() { "JT001", "JT000", "JT002", "JT003", "JT004", "JT005" } },
            new() { DescripcionCampo = "FECHA DE ELABORACIÓN:", ValoresPorSku = new() { "10/10/24", "09/10/24", "11/10/24", "12/10/24", "13/10/24", "14/10/24" } },
            new() { DescripcionCampo = "TANQUE:", ValoresPorSku = new() { "T-01", "T-02", "T-03", "T-04", "T-05", "T-06" } },
            new() { DescripcionCampo = "CLAVE:", ValoresPorSku = new() { "CL-01", "CL-02", "CL-03", "CL-04", "CL-05", "CL-06" } },
            new() { DescripcionCampo = "HORA INICIO DE LLENADO:", ValoresPorSku = new() { "08:00", "07:45", "08:15", "08:10", "08:05", "07:55" } },
            new() { DescripcionCampo = "HORA DE TERMINO LLENADO:", ValoresPorSku = new() { "09:00", "08:30", "09:10", "09:00", "08:55", "08:45" } },
            new() { DescripcionCampo = "VOLUMEN PREPARADO DE JARABE TERMINADO:", ValoresPorSku = new() { "1,000 L", "950 L", "1,100 L", "1,050 L", "1,000 L", "990 L" } },
            new() { DescripcionCampo = "VOLUMEN UTILIZADO DE JARABE TERMINADO:", ValoresPorSku = new() { "950 L", "900 L", "1,050 L", "1,000 L", "980 L", "960 L" } },
            new() { DescripcionCampo = "LOTE DE FRUCTOSA:", ValoresPorSku = new() { "F001", "F002", "F003", "F004", "F005", "F006" } },
            new() { DescripcionCampo = "LOTE DE AZUCAR:", ValoresPorSku = new() { "AZ001", "AZ002", "AZ003", "AZ004", "AZ005", "AZ006" } },
            new() { DescripcionCampo = "NÚMERO DE BATCH:", ValoresPorSku = new() { "B001", "B002", "B003", "B004", "B005", "B006" } },
            new() { DescripcionCampo = "LOTE DE AGUA TRATADA:", ValoresPorSku = new() { "AG001", "AG002", "AG003", "AG004", "AG005", "AG006" } },
            new() { DescripcionCampo = "LOTE DE CONCENTRADOS PARTE 1:", ValoresPorSku = new() { "C1-01", "C1-02", "C1-03", "C1-04", "C1-05", "C1-06" } },
            new() { DescripcionCampo = "LOTE DE CONCENTRADOS PARTE 2:", ValoresPorSku = new() { "C2-01", "C2-02", "C2-03", "C2-04", "C2-05", "C2-06" } },
            new() { DescripcionCampo = "PARTES LIQUIDAS Y CONSECUTIVAS DE BOLSAS:", ValoresPorSku = new() { "PLB-01", "PLB-02", "PLB-03", "PLB-04", "PLB-05", "PLB-06" } },
            new() { DescripcionCampo = "PARTES SECAS Y CONSECUTIVAS DE BOLSAS:", ValoresPorSku = new() { "PSB-01", "PSB-02", "PSB-03", "PSB-04", "PSB-05", "PSB-06" } },
        }
            };
        }
        

        public static BloqueJarabeSimpleModel ObtenerAnalisisFisicoquimicoJarabeTerminado(string lote)
        {
            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "Análisis Fisicoquímicos de Jarabe Terminado",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO" },
                Registros = new List<RegistroJarabeSimple>
        {
            new() { DescripcionCampo = "BRIX:", ValoresPorSku = new() { "10.5", "10.6", "10.4", "10.7", "10.3", "10.2" } },
            new() { DescripcionCampo = "P1:", ValoresPorSku = new() { "2.0", "2.1", "2.0", "2.2", "2.0", "2.1" } },
            new() { DescripcionCampo = "ACIDEZ:", ValoresPorSku = new() { "0.15", "0.16", "0.14", "0.15", "0.16", "0.15" } },
            new() { DescripcionCampo = "PH:", ValoresPorSku = new() { "3.5", "3.6", "3.4", "3.5", "3.6", "3.5" } },
            new() { DescripcionCampo = "TEMPERATURA:", ValoresPorSku = new() { "25°C", "26°C", "25°C", "27°C", "24°C", "25°C" } },
            new() { DescripcionCampo = "APARIENCIA:", ValoresPorSku = new() { "Transparente", "Transparente", "Ligeramente turbio", "Transparente", "Transparente", "Transparente" } },
            new() { DescripcionCampo = "OLOR:", ValoresPorSku = new() { "Normal", "Normal", "Normal", "Leve", "Normal", "Normal" } },
            new() { DescripcionCampo = "SABOR:", ValoresPorSku = new() { "Correcto", "Correcto", "Correcto", "Correcto", "Leve variación", "Correcto" } },
            new() { DescripcionCampo = "LOTE DE AGUA TRATADA:", ValoresPorSku = new() { "AG-JT-01", "AG-JT-02", "AG-JT-03", "AG-JT-04", "AG-JT-05", "AG-JT-06" } },
            new() { DescripcionCampo = "ANALISTA ENC. JARABES:", ValoresPorSku = new() { "Juan P.", "Ana R.", "Luis G.", "Marta C.", "Carlos S.", "Beatriz Z." } },
            new() { DescripcionCampo = "SUP. CALIDAD:", ValoresPorSku = new() { "Ricardo M.", "Laura D.", "Luis H.", "Carmen T.", "Silvia V.", "Alejandro B." } }
        }
            };
        }
        */

        public static BloqueJarabeSimpleModel ObtenerAnalisisSensorialJarabeTerminado(string lote)
        {
            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "Resultados de Análisis Sensorial del Jarabe Terminado",
                EncabezadosSku = new List<string> { "SKU QUEJA", "ANTERIOR", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "OTRO INVOLUCRADO", "RESULTADO PANEL" },
                Registros = new List<RegistroJarabeSimple>
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
                                    prop.SetValue(modelo, "N/A");
                            }

                            resultados.Add(modelo);
                        }
                    }
                }
                return resultados;
            }
        }


        public static BloqueJarabeSimpleModel ObtenerJarabeSimpleConContacto(
   List<TrazabilidadNode> trazabilidadNodos,
   long batchPadre,
   TimeSpan horaQueja)
        {
            var nodoPadre = trazabilidadNodos.FirstOrDefault(x => x.Batch == batchPadre);
            DateTime fechaProduccion = nodoPadre?.StartDate.Date ?? DateTime.Today;
            DateTime fechaHoraQueja = fechaProduccion + horaQueja;

            // 1. Obtener nodos de jarabe simple ordenados por hora
            var nodosJarabeSimple = trazabilidadNodos
                .Where(n => n.ManufacturingReferenceName.ToUpper().Contains("JARABE SIMPLE"))
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
        "TIPO DE AZÚCAR UTILIZADA:",
        "LOTE DE AZÚCAR:",
        "FECHA DE ELABORACIÓN DEL JARABE SIMPLE:",
        "VOLUMEN PREPARADO DE JARABE SIMPLE:",
        "VOLUMEN UTILIZADO DE JARABE SIMPLE:",
        "# TANQUE DISOLUTOR DONDE SE PREPARÓ EL JARABE SIMPLE:",
        "LOTE DE AGUA TRATADA UTILIZADA JAR. SIMPLE:",
        "LOTE DEL MATERIAL FILTRANTE (SOKALFLOC, etc):",
        "LOTE DE FILTRO BOLSA UTILIZADO:",
        "VOLUMEN PREPARADO:"
    };

            var registros = campos.Select(c => new RegistroJarabeSimple
            {
                DescripcionCampo = c,
                ValoresPorSku = new List<string>()
            }).ToList();

            // 6. Llenar los valores por columna (por lote)
            foreach (var lote in lotesSeleccionados)
            {
                if (lote == null)
                {
                    // Agrega solo "N/A" en todas las filas
                    foreach (var fila in registros)
                    {
                        fila.ValoresPorSku.Add("N/A");
                    }
                    continue;
                }

                var hijos = trazabilidadNodos
                    .Where(x => x.BatchPadre == lote.Batch)
                    .ToList();

                var azucar = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("AZUCAR"));
                var aguaTratada = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("AGUA TRATADA"));
                var materialFiltrante = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("FLOC")
                    || h.ManufacturingReferenceName.ToUpper().Contains("POLIACRILAMIDA")
                    || h.ManufacturingReferenceName.ToUpper().Contains("CELATOM")
                    || h.ManufacturingReferenceName.ToUpper().Contains("ARBOCEL"));
                var filtroBolsa = hijos.FirstOrDefault(h => h.ManufacturingReferenceName.ToUpper().Contains("FILTRO"));


                registros[0].ValoresPorSku.Add(lote.BatchName ?? "N/A");
                registros[1].ValoresPorSku.Add("N/A");
                registros[2].ValoresPorSku.Add(azucar?.ManufacturingReferenceName ?? "N/A");
                registros[3].ValoresPorSku.Add(azucar?.BatchName ?? "N/A");
                registros[4].ValoresPorSku.Add(lote.StartDate.ToString("dd/MM/yyyy HH:mm"));
                registros[5].ValoresPorSku.Add("N/A");
                registros[6].ValoresPorSku.Add("N/A");
                registros[7].ValoresPorSku.Add("N/A");
                registros[8].ValoresPorSku.Add(aguaTratada?.BatchName ?? "N/A");       // LOTE DE AGUA TRATADA
                registros[9].ValoresPorSku.Add(materialFiltrante?.BatchName ?? "N/A"); // LOTE DEL MATERIAL FILTRANTE
                registros[10].ValoresPorSku.Add(filtroBolsa?.BatchName ?? "N/A");      // LOTE DE FILTRO BOLSA

                registros[11].ValoresPorSku.Add("N/A");
            }


            return new BloqueJarabeSimpleModel
            {
                TituloBloque = "LOTE DE JARABE SIMPLE (NÚM. BATCH). MENCIONAR LOTE CORRESPONDIENTE A LA MUESTRA Y 2 ANTERIORES QUE PUDIERON HABER ESTADO EN CONTACTO",
                EncabezadosSku = encabezados,
                Registros = registros
            };
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

            // 1. Lotes de jarabe simple bb 
            var nodosJarabeSimple = trazabilidadNodos
                .Where(n => n.ManufacturingReferenceName.ToUpper().Contains(bloque))
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
                            registro.ValoresPorLote.Add("N/A");
                            continue;
                        }

                        var key = (Lote: lote.Batch, Op: registro.DescripcionParametro); // ambos ya homogeneizados
                        index.TryGetValue(key, out var resultado);

                        if (resultado == null)
                        {
                            registro.ValoresPorLote.Add("N/A");
                            continue;
                        }

                        string valorFinal;
                        if (resultado.TipoOperacion == 1)
                        {
                            valorFinal = string.IsNullOrWhiteSpace(resultado.Valor) ? "N/A" : resultado.Valor;
                        }
                        else if (resultado.TipoOperacion == 2)
                        {
                            valorFinal = resultado.Atributo == "1" ? "✔"
                                       : resultado.Atributo == "0" ? "✘"
                                       : "N/A";
                        }
                        else
                        {
                            valorFinal = "N/A";
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
