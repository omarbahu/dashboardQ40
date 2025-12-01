namespace dashboardQ40.Models
{
    // ✅ 1) Request que viene desde la pantalla (filtros + carátula)
    public class CertificadoRequestModel
    {
        // Filtros
        public string Pais { get; set; }
        public string Planta { get; set; }
        public string Line { get; set; }
        public string Sku { get; set; }
        public DateTime Fecha { get; set; }

        // Carátula (todo lo que el usuario teclea)
        public string LineaTexto { get; set; }
        public string Turno { get; set; }
        public string CodigoLote { get; set; }
        public string Sabor { get; set; }
        public string Apariencia { get; set; }
        public string PlantaSuministro { get; set; }
        public string AnalistasProceso { get; set; }
        public string SupervisorCalidad { get; set; }
        public string Analista { get; set; }
        public string JefeCalidad { get; set; }
        public string TamanoLoteTexto { get; set; }

        // Variables seleccionadas (códigos de VarY)
        public List<string> VariablesY { get; set; } = new();
    }

    // ✅ 2) Fila de la tabla de características (lo que se ve en el certificado)
    public class CertificadoCaracteristicaDto
    {
        public string Nombre { get; set; }           // Brix, Contenido neto, Torque...
        public int? Muestras { get; set; }           // Mtra
        public decimal? LEI { get; set; }
        public decimal? LES { get; set; }
        public decimal? Media { get; set; }          // Prom
        public decimal? Sigma { get; set; }          // Sig
        public decimal? PorcBajoLEI { get; set; }    // %Bjo LEI (lo calculamos después)
        public decimal? PorcSobreLES { get; set; }   // %Sre LES
        public decimal? Cpk { get; set; }
    }

    // ✅ 3) ViewModel final para la vista / PDF
    public class CertificadoCalidadViewModel
    {
        // Encabezado
        public string CompanyName { get; set; }      // Bebidas Mundiales...
        public string PlantName { get; set; }        // Planta Insurgentes / Cd Juarez
        public string Country { get; set; }
        public string City { get; set; }
        public string Address { get; set; }

        public string Comentario { get; set; }       // "Planta Insurgentes"
        public DateTime FechaImpresion { get; set; }

        // Datos de producto / lote
        public string NombreParte { get; set; }      // "05 Coca C.2.5 lt Ref Pet"
        public string Linea { get; set; }
        public string Turno { get; set; }
        public string CodigoProduccion { get; set; } // 20NOV25
        public string Lote { get; set; }
        public int? TamanoLoteCajas { get; set; }

        // Datos de personas
        public string Analista { get; set; }
        public string AnalistasProceso { get; set; }
        public string SupervisorCalidad { get; set; }
        public string JefeCalidad { get; set; }
        public string Sabor { get; set; }
        public string Apariencia { get; set; }
        public string PlantaSuministro { get; set; }
        public string TamanoLoteTexto { get; set; }  // "5000 CAJAS"


        // Tabla de estadísticas
        public List<CertificadoCaracteristicaDto> Caracteristicas { get; set; } = new();
    }
}
