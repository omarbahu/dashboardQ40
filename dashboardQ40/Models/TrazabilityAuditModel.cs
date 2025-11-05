namespace dashboardQ40.Models
{
 
    // Models/ReporteTrazabilidad.cs
    public class ReporteTrazabilidad
    {
        public int IdReporte { get; set; }
        public DateTime FechaHora { get; set; }
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFin { get; set; }
        public string MotivoTrazabilidad { get; set; } = string.Empty;
        public bool TrazaProductoMp { get; set; }
        public decimal? PorcEficProductoTerminado { get; set; }
        public string Lote { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string UsuarioVobo { get; set; } = string.Empty;
        public TimeSpan horaQueja { get; set; }
        public string company { get; set; } = string.Empty;
        public string? country { get; set; }  // país (código)
        public bool simulate { get; set; }
    }

    // Models/PersonaReporte.cs
    public class PersonaReporte
    {
        public int IdPersonaRep { get; set; }
        public int IdReporte { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public TimeSpan? Hora { get; set; }
        public string Puesto { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Actividad { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
    }
}


public class FormatoViewModel
{
    public string Lote { get; set; }

    public BloqueMateriaPrimaModel BloqueMateriaPrima { get; set; }
    public BloqueProductoTerminadoModel BloqueProductoTerminado { get; set; }
    public BloqueEntregaInformacionModel BloqueRegistroTiempo { get; set; }
    public BloqueLotesPrincipalesModel BloqueJarabesLoteJarabeSimple { get; set; }

    public BloqueLotesPrincipalesModel BloqueJarabesLoteJarabeSimpleContacto { get; set; }
    public BloqueLotesPrincipalesModel BloqueAnalisisSensorialJarabeSimple { get; set; }
    public BloqueLotesPrincipalesModel BloqueJarabeTerminado { get; set; }
    public BloqueAnalisisFisicoquimicoModel BloqueAnalisisFisicoquimicoJarabeTerminado { get; set; }
    public BloqueLotesPrincipalesModel BloqueAnalisisSensorialJarabeTerminado { get; set; }

    public List<PruebaLiberacionRow> BloquePruebasLiberacion { get; set; } = new();
    public List<LiberacionSensorialProductoModel> BloqueLiberacionSensorialProducto { get; set; } = new();
    public BloqueAnalisisFisicoquimicoModel BloqueAnalisisFisicoquimicoJarabeSimple { get; set; }
    public BloqueAnalisisFisicoquimicoModel BloqueCO2 { get; set; }

    public BloqueAnalisisFisicoquimicoModel BloqueAzucar { get; set; }
    public BloqueAnalisisFisicoquimicoModel BloqueFructuosa1 { get; set; }
    public BloqueLotesPrincipalesModel BloqueAguaTratada { get; set; }
    public BloqueAnalisisFisicoquimicoModel BloqueFructuosa2 { get; set; }
    public BloqueAnalisisFisicoquimicoModel BloqueFructuosa3 { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloquenitrogeno { get; set; }

    public BloqueAnalisisFisicoquimicoModel Bloqueaguatratadajarabesimple { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloqueaguatratadajarabeterminado { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloqueaguatratadaproductoterminado { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloqueaguatratadacruda { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloqueaguatratadasuave { get; set; }

    public BloqueLotesPrincipalesModel Bloquesaneo { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloquesaneojarabesimple { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloquesaneojarabeterminado { get; set; }
    public BloqueAnalisisFisicoquimicoModel Bloqueasaneoproductoterminado { get; set; }



    // ...
}


public class BloqueMateriaPrimaModel
{
    public List<RegistroMateriaPrima> Registros { get; set; }
    public string SupervisorCalidad { get; set; } = string.Empty;
    public string SupervisorAlmacen { get; set; } = string.Empty;
}

public class RegistroMateriaPrima
{
    public string Descripcion { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;
    public string LoteExterno { get; set; } = string.Empty;
    public string LoteInterno { get; set; } = string.Empty;
    public string FechaRecepcion { get; set; } = string.Empty;
    public string Cantidad { get; set; } = string.Empty;
}


public class BloqueJarabesModel
{
    public List<string> Columnas { get; set; }
    public List<List<string>> Filas { get; set; }
}

public class BloqueProductoTerminadoModel
{
    public string DescripcionProducto { get; set; } = string.Empty;
    public string CodigoProducto { get; set; } = string.Empty;
    public string EncargadoPruebas { get; set; } = string.Empty;
    public string SupervisorJarabes { get; set; } = string.Empty;
    public string VacioJarabeTerminado { get; set; } = string.Empty;
    public string SupervisorCalidad { get; set; } = string.Empty;
    public string FechaProduccion { get; set; } = string.Empty;// ej: "15/10/2024 al 16/10/2024"
    public string InicioProduccion { get; set; } = string.Empty;
    public string FinProduccion { get; set; } = string.Empty;
    public string SupervisorProduccion { get; set; } = string.Empty;
    public string SupervisorMantenimiento { get; set; } = string.Empty;
    public string Llenadora { get; set; } = string.Empty;
    public string CantidadElaborada { get; set; } = string.Empty;
    public string NumeroLinea { get; set; } = string.Empty;
}

public class RegistroEntregaInformacionModel
{
    public string NombrePersona { get; set; } = string.Empty;
    public string Hora { get; set; } = string.Empty;
    public string Puesto { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Actividad { get; set; } = string.Empty;
    public string MedioEntrega { get; set; } = string.Empty;// Papel o Electrónico
    public string Observaciones { get; set; } = string.Empty;
}

public class BloqueEntregaInformacionModel
{
    public List<RegistroEntregaInformacionModel> Registros { get; set; } = new();
}


public class RegistroLotesPrincipales
{
    public string DescripcionCampo { get; set; } = string.Empty;// Por ejemplo: "LOTE DE JARABE SIMPLE"
    public List<string> ValoresPorSku { get; set; } // Columnas: SKU queja, anterior, involucrado, etc.
}

public class BloqueLotesPrincipalesModel
{
    public string TituloBloque { get; set; } = string.Empty;
    public List<string> EncabezadosSku { get; set; } // SKU QUEJA, ANTERIOR, OTRO INVOLUCRADO, ...
    public List<RegistroLotesPrincipales> Registros { get; set; } = new();
}


public class PruebaLiberacionModel
{
    public string Parametro { get; set; } = string.Empty;
    public string InicioCorrida { get; set; } = string.Empty;
    public string MedioCorrida { get; set; } = string.Empty;
    public string FinCorrida { get; set; } = string.Empty;
}


public class LiberacionSensorialProductoModel
{
    public string Panelista { get; set; } = string.Empty;
    public string FechaLiberacion { get; set; } = string.Empty;
    public string Producto { get; set; } = string.Empty;
    public string CodigoMTRA { get; set; } = string.Empty;
    public string Referencia { get; set; } = string.Empty;
    public string ResultadoPanel { get; set; } = string.Empty;
}

public class TrazabilidadNode
{
    public string Padre { get; set; } = string.Empty;
    public string Hijo { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string ManufacturingReference { get; set; } = string.Empty;
    public string ManufacturingFamily { get; set; } = string.Empty;
    public string manufacturingFamilyName { get; set; } = string.Empty;
    public string ManufacturingReferenceName { get; set; } = string.Empty;
    public string workplacename { get; set; } = string.Empty;
    public string workplace { get; set; } = string.Empty;
    public decimal bcquantity { get; set; }
    public decimal consumedquantity { get; set; }    
    public long Batch { get; set; }
    public long BatchPadre { get; set; }
    public string BatchIdentifier { get; set; } = string.Empty;
    public string BatchName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsRawMaterial { get; set; }
    public int Nivel { get; set; }
}

public class LoteDescripcionInfo
{
    public string BatchName { get; set; } = string.Empty;
    public string ManufacturingReferenceName { get; set; } = string.Empty;
}

public class BloqueAnalisisFisicoquimicoModel
{
    public string TituloBloque { get; set; } = string.Empty;
    public List<string> EncabezadosSku { get; set; } // SKU QUEJA, ANTERIOR, ETC.
    public List<RegistroAnalisisFisicoquimico> Registros { get; set; }
}

public class RegistroAnalisisFisicoquimico
{
    public string DescripcionParametro { get; set; } = string.Empty;// pH, BRIX, TURBIDEZ...
    public List<string> ValoresPorLote { get; set; }  // columnas dinámicas
}

public class ResultadoAnalisisFisicoquimico
{
    public long Lote { get; set; }               // 🔁 batch
    public string OperacionNombre { get; set; } = string.Empty; // controlOperationName
    public string Atributo { get; set; } = string.Empty;       // resultAttribute
    public string Valor { get; set; } = string.Empty;           // resultValue
    public int TipoOperacion { get; set; }
}

public class PruebaLiberacionRow
{
    public string ControlOperationName { get; set; } = string.Empty;
    public string InicioCorrida { get; set; } = string.Empty;
    public string MedioCorrida { get; set; } = string.Empty;
    public string FinCorrida { get; set; } = string.Empty;
}

public class ResultadoAnalisisFisicoquimicoByDate
{
    public string OperacionNombre { get; set; } = string.Empty;
    public byte? TipoOperacion { get; set; }        // mapeado a TipoOperacionCode
    public string TipoOperacionTexto { get; set; } = string.Empty;
    public string InicioValor { get; set; } = string.Empty;
    public DateTime? InicioFecha { get; set; }
    public string MedioValor { get; set; } = string.Empty;
    public DateTime? MedioFecha { get; set; }
    public string FinValor { get; set; } = string.Empty;
    public DateTime? FinFecha { get; set; }
}

public class HistorialReporteItem
{
    public int IdReporte { get; set; }
    public DateTime FechaHora { get; set; }
    public string Lote { get; set; }
    public string PlantCode { get; set; }
    public string Motivo { get; set; }
    public decimal? PorcEficPT { get; set; }
    public string Revision { get; set; }
    public string Status { get; set; }
    public TimeSpan? HoraQueja { get; set; }
}

public class HistorialReporteLookup
{
    public string Lote { get; set; }
    public TimeSpan? HoraQueja { get; set; }
}


