using dashboardQ40.Models;
using static dashboardQ40.Models.Models;   // para AutocontrolExcelRow
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace dashboardQ40.Services
{
    public static class AutocontrolPayloadBuilder
    {
        /// <summary>
        /// Construye 1 payload por IdControlProcedureResult,
        /// agrupando operaciones y valores.
        /// </summary>
        public static List<ControlProcedureResultPayload> BuildFromExcelRows(List<AutocontrolExcelRow> filas)
        {
            var result = new List<ControlProcedureResultPayload>();
            if (filas == null || filas.Count == 0)
                return result;

            // 1) Agrupamos por idControlProcedureResult (1 JSON por autocontrol)
            var gruposCPR = filas.GroupBy(f => f.IdControlProcedureResult);

            foreach (var grp in gruposCPR)
            {
                var first = grp.First();

                // Helpers para parsear
                int ToInt(string s, int def = 0) =>
                    int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : def;

                bool ToBool(string s) =>
                    s != null && (s == "1" ||
                    s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("sí", StringComparison.OrdinalIgnoreCase) ||
                    s.Equals("si", StringComparison.OrdinalIgnoreCase));

                decimal? ToDecimal(string s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                        return v;
                    return null;
                }

                string FormatDate(string s)
                {
                    if (string.IsNullOrWhiteSpace(s)) return null;
                    if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
                        return d.ToString("yyyy-MM-ddTHH:mm:ss");
                    return null;
                }

                var payload = new ControlProcedureResultPayload
                {
                    ManufacturingOrder = first.ManufacturingOrder,
                    ManufacturingPhase = ToInt(first.ManufacturingPhase),
                    Batch = string.IsNullOrWhiteSpace(first.Batch) ? null : first.Batch,
                    IdControlProcedureResult = first.IdControlProcedureResult,
                    IsManual = ToBool(first.IsManual),
                    Workplace = first.Workplace,
                    LaunchingDate = FormatDate(first.LaunchingDate),
                    ControlProcedure = first.ControlProcedure,
                    ControlProcedureVersion = first.ControlProcedureVersion,
                    ControlProcedureLevel = ToInt(first.ControlProcedureLevel),
                    ControlProcedureNote = first.ControlProcedureNote,
                    Worker = first.Worker
                };

                // 2) Dentro de cada CPR, agrupamos por operación
                var gruposOperacion = grp.GroupBy(f => new
                {
                    f.ControlOperation,
                    f.ControlOperationNote,
                    f.DoesNotApply
                });

                foreach (var gOp in gruposOperacion)
                {
                    var op = new OperationResultPayload
                    {
                        ControlOperation = gOp.Key.ControlOperation,
                        ControlOperationNote = gOp.Key.ControlOperationNote,
                        DoesNotApply = ToBool(gOp.Key.DoesNotApply)
                    };

                    foreach (var fila in gOp)
                    {
                        var val = new ValuePayload
                        {
                            ResultAttribute = ToInt(fila.ResultAttribute),
                            ResultNumber = ToInt(fila.ResultNumber),
                            ResultValue = ToDecimal(fila.ResultValue),
                            ResultPresetAttributeValue = string.IsNullOrWhiteSpace(fila.ResultPresetAttributeValue) ? null : fila.ResultPresetAttributeValue,
                            ControlOperationResultValueNote = fila.ControlOperationResultValueNote
                        };

                        op.Values.Add(val);
                    }

                    payload.OperationResults.Add(op);
                }

                result.Add(payload);
            }

            return result;
        }

        /// <summary>
        /// Azúcar sintáctica: ordena filas y delega en BuildFromExcelRows.
        /// </summary>
        public static List<ControlProcedureResultPayload> BuildOnePerControlProcedureResult(
            List<AutocontrolExcelRow> filas)
        {
            if (filas == null || filas.Count == 0)
                return new List<ControlProcedureResultPayload>();

            var filasOrdenadas = filas
                .OrderBy(f => f.IdControlProcedureResult)
                .ThenBy(f => f.ControlProcedure)
                .ThenBy(f => f.ControlOperation)
                .ThenBy(f => f.ResultNumber)
                .ToList();

            return BuildFromExcelRows(filasOrdenadas);
        }
    }
}
