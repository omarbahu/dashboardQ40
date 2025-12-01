using System;
using System.Collections.Generic;

namespace dashboardQ40.Models
{
    /// <summary>
    /// Request que viene del carrito de la pantalla de límites.
    /// </summary>
    public class ApplyLimitsRequest
    {
        /// <summary>
        /// Compañía Captor (ej. "001").
        /// </summary>
        public string Company { get; set; } = string.Empty;

        /// <summary>
        /// Fecha/hora en la que quieres que entre en vigor la nueva versión.
        /// Puede venir null si se decide en el backend.
        /// </summary>
        public DateTime? ActivationDate { get; set; }

        /// <summary>
        /// Si true, la nueva versión se marca como activa inmediatamente
        /// (detalle lo afinamos después).
        /// </summary>
        public bool LaunchNow { get; set; }

        /// <summary>
        /// Normas seleccionadas en el carrito con sus nuevos límites.
        /// </summary>
        public List<ApplyLimitsItem> Items { get; set; } = new();
    }

    /// <summary>
    /// Una norma seleccionada en el carrito.
    /// </summary>
    public class ApplyLimitsItem
    {
        /// <summary>
        /// Código del Control Procedure (ej. "JT-CC50-01").
        /// </summary>
        public string ControlProcedure { get; set; } = string.Empty;

        /// <summary>
        /// Versión actual desde la que vamos a clonar (ej. "4").
        /// </summary>
        public string CurrentVersion { get; set; } = string.Empty;

        /// <summary>
        /// Nivel actual desde el que clonamos (ej. 2).
        /// </summary>
        public short CurrentLevel { get; set; }

        /// <summary>
        /// Operación de control (norma) a la que se le ajustan los límites.
        /// Coincide con ControlProcedureOperation.controlOperation.
        /// </summary>
        public string ControlOperation { get; set; } = string.Empty;

        /// <summary>
        /// Nuevo límite inferior (LSL) sugerido por el análisis.
        /// </summary>
        public double? NewLsl { get; set; }

        /// <summary>
        /// Nuevo límite superior (USL) sugerido por el análisis.
        /// </summary>
        public double? NewUsl { get; set; }
    }


    // ... ApplyLimitsRequest y ApplyLimitsItem arriba

    /// <summary>
    /// Fila de la tabla ControlProcedure tal como viene del REST/TableRow.
    /// Solo mapeamos las columnas que vamos a usar.
    /// </summary>
    public class ControlProcedureRow
    {
        public string company { get; set; } = string.Empty;
        public string controlProcedure { get; set; } = string.Empty;
        public string controlProcedureVersion { get; set; } = string.Empty;
        public short controlProcedureLevel { get; set; }

        public string? controlProcedureLevelName { get; set; }
        public string? controlProcedureLevelDescrip { get; set; }

        public bool showInTerminal { get; set; }
        public bool isManual { get; set; }
        public string? controlProcedureType { get; set; }
        public bool manualAffectNext { get; set; }
        public string recordState { get; set; } = "OP";

        public bool executedAffectNext { get; set; }
        public bool allowPartialReporting { get; set; }
        public byte controlProcedureClass { get; set; }
        public bool allowClosingIncompleteCP { get; set; }
        public bool expireOnRelaunch { get; set; }
        public bool expireOnPhaseClosing { get; set; }
        public bool expireOnShiftChange { get; set; }
        public bool expireOnPhaseRemoval { get; set; }

        public int? expirationTime { get; set; }
        public bool workplacePresenceRequired { get; set; }
        public bool passwordRequired { get; set; }
        public bool lockHigherRankLevels { get; set; }

        public byte emptyCPROSavingBehaviour { get; set; }

        // Campos de auditoría (opcionales, por si luego los usamos)
        public string? creationUser { get; set; }
        public DateTime? creationData { get; set; }
        public string? lastUpdateUser { get; set; }
        public DateTime? lastUpdateData { get; set; }
    }


    /// <summary>
    /// Fila de la tabla ControlProcedureOperation (solo campos relevantes).
    /// </summary>
    public class ControlProcedureOperationRow
    {
        public string company { get; set; } = string.Empty;
        public string controlOperation { get; set; } = string.Empty;
        public string controlProcedure { get; set; } = string.Empty;
        public string controlProcedureVersion { get; set; } = string.Empty;
        public short controlProcedureLevel { get; set; }

        public short position { get; set; }
        public bool notApplyAllowed { get; set; }

        public string controlOperationName { get; set; } = string.Empty;
        public byte controlOperationType { get; set; }

        public string? measuringInstrument { get; set; }
        public string? measuringUnit { get; set; }
        public byte? unitPrecision { get; set; }

        // Límites / tolerancias (los importantes para nosotros)
        public decimal? nominalValue { get; set; }
        public decimal? maxTolerance { get; set; }      // USL (aprox)
        public decimal? minTolerance { get; set; }      // LSL (aprox)
        public decimal? clientMinValue { get; set; }
        public decimal? clientMaxValue { get; set; }

        public decimal? rangeGraphMaxValue { get; set; }
        public byte sampleSize { get; set; }

        public string? controlOperationDescription { get; set; }

        public decimal? tareMeasurement { get; set; }
        public decimal? range { get; set; }
        public short sampleGroupingNumber { get; set; }

        public byte previousVisibleResultsNumber { get; set; }
        public byte? toleranceMode { get; set; }

        public decimal? maxTolerancePercentage { get; set; }
        public decimal? minTolerancePercentage { get; set; }
        public decimal? clientMinValuePercentage { get; set; }
        public decimal? clientMaxValuePercentage { get; set; }

        public string? controlOperationClass { get; set; }
        public bool hasControlOperationFormula { get; set; }
        public string? controlOperationFormula { get; set; }
        public string? navigationPath { get; set; }

        public bool? ignoreCase { get; set; }
        public byte? undefinedAttributeValue { get; set; }

        // Auditoría (por si luego los usamos)
        public string? creationUser { get; set; }
        public DateTime? creationData { get; set; }
        public string? lastUpdateUser { get; set; }
        public DateTime? lastUpdateData { get; set; }
    }


}
