using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace dashboardQ40.Models
{
    // Raíz del JSON que espera CompleteControlProcedure
    public class ControlProcedureResultPayload
    {
        [JsonProperty("manufacturingOrder")]
        public string ManufacturingOrder { get; set; }

        [JsonProperty("manufacturingPhase")]
        public int ManufacturingPhase { get; set; }

        [JsonProperty("batch")]
        public string Batch { get; set; }

        [JsonProperty("idControlProcedureResult")]
        public string IdControlProcedureResult { get; set; }

        [JsonProperty("isManual")]
        public bool IsManual { get; set; }

        [JsonProperty("workplace")]
        public string Workplace { get; set; }

        [JsonProperty("launchingDate")]
        public string LaunchingDate { get; set; }  // lo mandamos como string ISO o null

        [JsonProperty("controlProcedure")]
        public string ControlProcedure { get; set; }

        [JsonProperty("controlProcedureVersion")]
        public string ControlProcedureVersion { get; set; }

        [JsonProperty("controlProcedureLevel")]
        public int ControlProcedureLevel { get; set; }

        [JsonProperty("controlProcedureNote")]
        public string ControlProcedureNote { get; set; }

        [JsonProperty("worker")]
        public string Worker { get; set; }

        [JsonProperty("operationResults")]
        public List<OperationResultPayload> OperationResults { get; set; } = new();

        [JsonProperty("context")]
        public object Context { get; set; } = null;
    }

    public class OperationResultPayload
    {
        [JsonProperty("values")]
        public List<ValuePayload> Values { get; set; } = new();

        [JsonProperty("controlOperation")]
        public string ControlOperation { get; set; }

        [JsonProperty("controlOperationNote")]
        public string ControlOperationNote { get; set; }

        [JsonProperty("doesNotApply")]
        public bool DoesNotApply { get; set; }
    }

    public class ValuePayload
    {
        // OJO: aquí el JSON se llama resultAtribute (sin 2a t)
        [JsonProperty("resultAtribute")]
        public int ResultAttribute { get; set; }

        [JsonProperty("resultValue")]
        public decimal? ResultValue { get; set; }

        [JsonProperty("resultPresetAttributeValue")]
        public string ResultPresetAttributeValue { get; set; }

        [JsonProperty("resultNumber")]
        public int ResultNumber { get; set; }

        [JsonProperty("controlOperationResultValueNote")]
        public string ControlOperationResultValueNote { get; set; }
    }
}
