namespace dashboardQ40.Models
{
 
    // Models/ReporteTrazabilidad.cs
    public class ReporteTrazabilidad
    {
        public int IdReporte { get; set; }
        public DateTime FechaHora { get; set; }
        public TimeSpan? HoraInicio { get; set; }
        public TimeSpan? HoraFin { get; set; }
        public string MotivoTrazabilidad { get; set; }
        public bool TrazaProductoMp { get; set; }
        public decimal? PorcEficProductoTerminado { get; set; }
        public string Lote { get; set; }
        public string Revision { get; set; }
        public string UsuarioVobo { get; set; }
        public TimeSpan horaQueja { get; set; }
        public string company { get; set; }
        public bool simulate { get; set; }
    }

    // Models/PersonaReporte.cs
    public class PersonaReporte
    {
        public int IdPersonaRep { get; set; }
        public int IdReporte { get; set; }
        public string Nombre { get; set; }
        public TimeSpan? Hora { get; set; }
        public string Puesto { get; set; }
        public string Area { get; set; }
        public string Actividad { get; set; }
        public string Observaciones { get; set; }
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
    public string SupervisorCalidad { get; set; }
    public string SupervisorAlmacen { get; set; }
}

public class RegistroMateriaPrima
{
    public string Descripcion { get; set; }
    public string Proveedor { get; set; }
    public string LoteExterno { get; set; }
    public string LoteInterno { get; set; }
    public string FechaRecepcion { get; set; }
    public string Cantidad { get; set; }
}


public class BloqueJarabesModel
{
    public List<string> Columnas { get; set; }
    public List<List<string>> Filas { get; set; }
}

public class BloqueProductoTerminadoModel
{
    public string DescripcionProducto { get; set; }
    public string CodigoProducto { get; set; }
    public string EncargadoPruebas { get; set; }
    public string SupervisorJarabes { get; set; }
    public string VacioJarabeTerminado { get; set; }
    public string SupervisorCalidad { get; set; }
    public string FechaProduccion { get; set; } // ej: "15/10/2024 al 16/10/2024"
    public string InicioProduccion { get; set; }
    public string FinProduccion { get; set; }
    public string SupervisorProduccion { get; set; }
    public string SupervisorMantenimiento { get; set; }
    public string Llenadora { get; set; }
    public string CantidadElaborada { get; set; }
    public string NumeroLinea { get; set; }
}

public class RegistroEntregaInformacionModel
{
    public string NombrePersona { get; set; }
    public string Hora { get; set; }
    public string Puesto { get; set; }
    public string Area { get; set; }
    public string Actividad { get; set; }
    public string MedioEntrega { get; set; } // Papel o Electrónico
    public string Observaciones { get; set; }
}

public class BloqueEntregaInformacionModel
{
    public List<RegistroEntregaInformacionModel> Registros { get; set; } = new();
}


public class RegistroLotesPrincipales
{
    public string DescripcionCampo { get; set; } // Por ejemplo: "LOTE DE JARABE SIMPLE"
    public List<string> ValoresPorSku { get; set; } // Columnas: SKU queja, anterior, involucrado, etc.
}

public class BloqueLotesPrincipalesModel
{
    public string TituloBloque { get; set; } 
    public List<string> EncabezadosSku { get; set; } // SKU QUEJA, ANTERIOR, OTRO INVOLUCRADO, ...
    public List<RegistroLotesPrincipales> Registros { get; set; } = new();
}


public class PruebaLiberacionModel
{
    public string Parametro { get; set; }
    public string InicioCorrida { get; set; }
    public string MedioCorrida { get; set; }
    public string FinCorrida { get; set; }
}


public class LiberacionSensorialProductoModel
{
    public string Panelista { get; set; }
    public string FechaLiberacion { get; set; }
    public string Producto { get; set; }
    public string CodigoMTRA { get; set; }
    public string Referencia { get; set; }
    public string ResultadoPanel { get; set; }
}

public class TrazabilidadNode
{
    public string Padre { get; set; }
    public string Hijo { get; set; }
    public string Company { get; set; }
    public string ManufacturingReference { get; set; }
    public string ManufacturingFamily { get; set; }
    public string manufacturingFamilyName { get; set; }    
    public string ManufacturingReferenceName { get; set; }
    public string workplacename { get; set; }
    public decimal bcquantity { get; set; }
    public decimal consumedquantity { get; set; }    
    public long Batch { get; set; }
    public long BatchPadre { get; set; }
    public string BatchIdentifier { get; set; }
    public string BatchName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsRawMaterial { get; set; }
    public int Nivel { get; set; }
}

public class LoteDescripcionInfo
{
    public string BatchName { get; set; }
    public string ManufacturingReferenceName { get; set; }
}

public class BloqueAnalisisFisicoquimicoModel
{
    public string TituloBloque { get; set; }
    public List<string> EncabezadosSku { get; set; } // SKU QUEJA, ANTERIOR, ETC.
    public List<RegistroAnalisisFisicoquimico> Registros { get; set; }
}

public class RegistroAnalisisFisicoquimico
{
    public string DescripcionParametro { get; set; } // pH, BRIX, TURBIDEZ...
    public List<string> ValoresPorLote { get; set; }  // columnas dinámicas
}

public class ResultadoAnalisisFisicoquimico
{
    public long Lote { get; set; }               // 🔁 batch
    public string OperacionNombre { get; set; }  // controlOperationName
    public string Atributo { get; set; }         // resultAttribute
    public string Valor { get; set; }            // resultValue
    public int TipoOperacion { get; set; }
}

public class PruebaLiberacionRow
{
    public string ControlOperationName { get; set; }
    public string InicioCorrida { get; set; }
    public string MedioCorrida { get; set; }
    public string FinCorrida { get; set; }
}