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
