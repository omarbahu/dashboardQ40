using ClosedXML.Excel;
using dashboardQ40.Controllers;
using dashboardQ40.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;          // <- importante para .Where, ToDictionary, etc.


using static dashboardQ40.Models.Models;

public static class AutocontrolExcelReader
{
    public static List<AutocontrolExcelRow> LeerDesdeExcel(Stream excelStream)
    {
        var resultado = new List<AutocontrolExcelRow>();

        using var workbook = new XLWorkbook(excelStream);
        var ws = workbook.Worksheet(1); // primera hoja

        // Leemos encabezados (fila 1)
        var headerRow = ws.Row(1);
        var headerMap = headerRow.Cells()
            .Where(c => !string.IsNullOrWhiteSpace(c.GetString()))
            .ToDictionary(
                c => c.GetString().Trim(),          // nombre de la columna tal como está en Excel
                c => c.Address.ColumnNumber);       // número de columna

        // Función pequeña para leer una celda por nombre de columna
        string Get(IXLRow row, string columnName)
        {
            if (!headerMap.TryGetValue(columnName, out var colIndex))
                return null;

            return row.Cell(colIndex).GetString(); // aquí ya es IXLRow, sí tiene .Cell()
        }

        // Recorremos todas las filas con datos
        int lastRow = ws.LastRowUsed().RowNumber();

        for (int r = 2; r <= lastRow; r++) // empezamos en la fila 2 (datos)
        {
            var row = ws.Row(r);
            if (row.IsEmpty()) continue;

            var item = new AutocontrolExcelRow
            {
                Batch = Get(row, "batch"),
                ManufacturingOrder = Get(row, "manufacturingOrder"),
                ManufacturingPhase = Get(row, "manufacturingPhase"),
                IdControlProcedureResult = Get(row, "idControlProcedureResult"),
                IsManual = Get(row, "isManual"),
                Workplace = Get(row, "workplace"),
                LaunchingDate = Get(row, "launchingDate"),
                ControlProcedure = Get(row, "controlProcedure"),
                ControlProcedureVersion = Get(row, "controlProcedureVersion"),
                ControlProcedureLevel = Get(row, "controlProcedureLevel"),
                ControlProcedureNote = Get(row, "controlProcedureNote"),
                Worker = Get(row, "worker"),

                ControlOperation = Get(row, "controlOperation"),
                ControlOperationNote = Get(row, "controlOperationNote"),
                DoesNotApply = Get(row, "doesNotApply"),

                ResultAttribute = Get(row, "resultAttribute"),
                ResultNumber = Get(row, "resultNumber"),
                ResultValue = Get(row, "resultValue"),
                ResultPresetAttributeValue = Get(row, "resultPresetAttributeValue"),
                ControlOperationResultValueNote = Get(row, "controlOperationResultValueNote")
            };

            resultado.Add(item);
        }

        return resultado;
    }
}

