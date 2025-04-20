namespace dashboardQ40.Models
{
    public class VariableEstadistica
    {
        public string Lote { get; set; }
        public string Variable { get; set; }
        public int Conteo { get; set; }
        public double Media { get; set; }
        public double Sigma { get; set; }
        public double LSL { get; set; }
        public double USL { get; set; }
        public double Cp { get; set; }
        public double Cpk { get; set; }
        public int Defectos { get; set; }
    }
}
