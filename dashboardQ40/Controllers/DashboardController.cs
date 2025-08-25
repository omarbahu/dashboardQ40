using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Text.Json;
using static dashboardQ40.Models.Models;

namespace dashboardQ40.Controllers
{
    public class DashboardController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;
        private readonly Dictionary<string, string> _variablesY;
        private readonly ILogger<DashboardController> _logger;
    

        public DashboardController(IOptions<WebServiceSettings> settings, AuthService authService, IConfiguration configuration, ILogger<DashboardController> logger)
        {
            _settings = settings.Value;
            _authService = authService;
            _logger = logger;

            // 📌 Cargar la configuración manualmente
            _variablesY = configuration.GetSection("VariablesY").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

       
        }
        public async Task<IActionResult> IndexAsync()
        {

            var token = await _authService.ObtenerTokenCaptor();
            if (token != null)
            {
                HttpContext.Session.SetString("AuthToken", token.access_token); // Guardar en sesión
            }

            var ListLineas = new List<result_lineas>();
            var lineas = new result_lineas();
            if (token != null)
            {
               

                Task<result_Q_Lineas> dataResultP = getDataQuality.getLinesByCompany(
                        token.access_token.ToString(),
                        _settings.QueryLineas,
                        _settings.Company,
                        _settings.trazalog);
                await Task.WhenAll(dataResultP);

                if (dataResultP.Result.result != null)
                {
                    foreach (var item in dataResultP.Result.result)
                    {
                        var linea = new result_lineas
                        {
                            workplace = item.workplace,
                            workplaceName = item.workplaceName,
                            workMode = item.workMode
                        };

                        ListLineas.Add(linea);
                    }

                }

            }
                // Simulando datos para los selectores
            ViewBag.Lines = ListLineas;
            ViewBag.Products = new List<string> { "600 ml CC REGULAR", "600 ml CC LIGHT", "600 ml CC SIN AZUCAR", "500 ml CC REGULAR" };

            // Variables Y y sus respectivas Variables X asociadas
            ViewBag.Variables = new Dictionary<string, List<string>>
            {
                { "BRIX BEBIDA", new List<string> { "BRIX BEBIDA", "FRECUENCIA BOMBA DE AGUA", "FRECUENCIA BOMBA DE JARABE", "FRECUENCIA DE BOMBA DE MEZCLA" } },
                { "CARBONATACIÓN", new List<string> { "TEMPERATURA", "PRESIÓN CO2", "POSICION DE MICRO", "SUMINISTRO DE PRESIÓN", "MAZELI", "PRESIÓN ENTRADA DEL SUBACARD", "PRESIÓN SALIDA DEL SUBACARD", "PRESIÓN DE CO2 EN SUBCARB" } }
            };

            ViewBag.produccion = _settings.Produccion;

            return View();
        }

        public IActionResult Resumen()
        {
            // Simulando datos para los selectores
            ViewBag.Lines = new List<string> { "Línea 7", "Línea 2", "Línea 3" };
            ViewBag.Products = new List<string> { "600 ml CC REGULAR", "600 ml CC LIGHT", "600 ml CC SIN AZUCAR", "500 ml CC REGULAR" };

            // Variables Y y sus respectivas Variables X asociadas
            ViewBag.Variables = new Dictionary<string, List<string>>
            {
                { "BRIX BEBIDA", new List<string> { "BRIX BEBIDA", "FRECUENCIA BOMBA DE AGUA", "FRECUENCIA BOMBA DE JARABE", "FRECUENCIA DE BOMBA DE MEZCLA" } },
                { "CARBONATACIÓN", new List<string> { "TEMPERATURA", "PRESIÓN CO2", "POSICION DE MICRO", "SUMINISTRO DE PRESIÓN", "MAZELI", "PRESIÓN ENTRADA DEL SUBACARD", "PRESIÓN SALIDA DEL SUBACARD", "PRESIÓN DE CO2 EN SUBCARB" } }
            };

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSelection(
     string line,
     DateTime startDate,
     DateTime endDate,
     string product,
     string variableY,
     List<string> variablesX)
        {
            string token = HttpContext.Session.GetString("AuthToken"); // Obtener el token de la sesión

            var result_Resultados = new List<result_Resultados>();
            var random = new Random();

            // 📌 Diccionario para almacenar los valores de Variables X
            //var variablesXData = new Dictionary<string, (string Nombre, List<double> Valores)>();
            var variablesXData = new Dictionary<string, List<double>>();

            if (variablesX != null && variablesX.Count > 0)
            {
                foreach (var variableX in variablesX)
                {
                    var dataResultP = getDataQuality.getResultsByVarX(
                        token.ToString(),
                        _settings.QueryResultVarY_X,
                        _settings.Company,
                        line, startDate, endDate, variableX);
                    await Task.WhenAll(dataResultP);

                    if (dataResultP.Result.result != null)
                    {
                        string nombreVariableX = dataResultP.Result.result.FirstOrDefault()?.controlOperationName ?? "Desconocido";


                        var values = dataResultP.Result.result
                            .Where(r => r.resultValue != null)
                            .Select(r => Convert.ToDouble(r.resultValue))
                            .ToList();

                        // Si no hay datos en BD, generar aleatorios
                        if (!values.Any())
                        {
                            values = Enumerable.Range(0, 30).Select(_ => (double)random.Next(1, 100)).ToList();
                        }

                        // 📌 Guardar en el diccionario con el ID de la Variable X como clave y el nombre + valores como datos
                        variablesXData[nombreVariableX] = values;
                    }
                }
            }

            var VariableYEnv = "";
            var VariableYName = "";
            if (!_settings.Produccion)
            {
                //VariableYEnv = variableY;
                switch (variableY)
                {
                    case "CB-":
                        VariableYEnv = "A-L7-2";
                        VariableYName = "Carbonatacion";
                        break;
                    case "BX-":
                        VariableYEnv = "A-L7-1";
                        VariableYName = "Brix";
                        break;
                    case "CN-":
                        VariableYEnv = "A-L7-3";
                        VariableYName = "Contenido Neto";
                        break;
                }
            } else
            {
                VariableYEnv = variableY;
            }
            // 📌 Definir equivalencias para Variable Y
            
          

            // 📌 Obtener los datos de la Variable Y
            var dataResultY = getDataQuality.getResultsByVarX(
                token.ToString(),
                _settings.QueryResultVarY_X,
                _settings.Company,
                line, startDate, endDate, VariableYEnv);
            await Task.WhenAll(dataResultY);

            // 📌 Extraer valores de la Variable Y
            var scatterDataY = new List<object>();
            double minY = 0, maxY = 10;

            if (dataResultY.Result.result != null)
            {
                scatterDataY = dataResultY.Result.result
                    .Where(r => r.resultValue != null && r.executionDate != null)
                    .OrderBy(r => r.executionDate)
                    .Select(r => new {
                        Time = r.executionDate.ToString("dd-MM-yy HH:mm"),
                        Value = r.resultValue ?? 0
                    })
                    .ToList<object>();

                // 📌 Obtener los valores min y max de la variable Y
                minY = dataResultY.Result.result.Min(r => r.minTolerance ?? r.resultValue ?? 0);
                maxY = dataResultY.Result.result.Max(r => r.maxTolerance ?? r.resultValue ?? 0);
            }

            // 📌 Guardar en ViewBag para la vista
            ViewBag.ScatterDataY = scatterDataY;
            ViewBag.VariablesXData = variablesXData;
            ViewBag.VariableY = variableY;
            ViewBag.VariableYName = VariableYName;
            ViewBag.SelectedXVariables = variablesX;
            ViewBag.MinThreshold = minY;
            ViewBag.MaxThreshold = maxY;
            ViewBag.Dates = scatterDataY.Select(d => ((dynamic)d).Time).ToList(); // Extrae las fechas en string

            _logger.LogInformation("▶️ Variable Y: {VariableY}", variableY);
            _logger.LogInformation("🔢 Total puntos Variable Y: {Count}", scatterDataY.Count);
            _logger.LogInformation("🧪 Total Variables X: {Count}", variablesXData.Keys.Count);

            // Si quieres detalles
            foreach (var kvp in variablesXData)
            {
                _logger.LogInformation("📈 Variable X: {Nombre} - Total puntos: {Cantidad}", kvp.Key, kvp.Value.Count);
            }

            if (scatterDataY.Count == 0)
            {
                _logger.LogWarning("⚠️ No se obtuvieron datos para la Variable Y: {VariableYEnv}", VariableYEnv);
            }
            return View("Result");
        }


        [HttpPost]
        public async Task<IActionResult> SubmitSelectionDetail(
    string line,
    DateTime startDate,
    DateTime endDate,
    string product,
    string variableY,
    List<string> variablesX)
        {
            string token = HttpContext.Session.GetString("AuthToken");
            var result_Resultados = new List<result_Resultados>();
            var random = new Random();
            var variablesXData = new Dictionary<string, List<double>>();

            if (variablesX != null && variablesX.Count > 0)
            {
                foreach (var variableX in variablesX)
                {
                    var dataResultP = getDataQuality.getResultsByVarX(
                        token, _settings.QueryResultVarY_X, _settings.Company,
                        line, startDate, endDate, variableX);
                    await Task.WhenAll(dataResultP);

                    if (dataResultP.Result.result != null)
                    {
                        var values = dataResultP.Result.result
                            .Where(r => r.resultValue != null)
                            .Select(r => Convert.ToDouble(r.resultValue))
                            .ToList();

                        if (!values.Any()) values = Enumerable.Range(0, 30).Select(_ => (double)random.Next(1, 100)).ToList();
                        variablesXData[variableX] = values;
                    }
                }
            }

            var VariableYEnv = "";
            var VariableYName = "";
            if (!_settings.Produccion)
            {
                //VariableYEnv = variableY;
                switch (variableY)
                {
                    case "CB-":
                        VariableYEnv = "A-L7-2";
                        VariableYName = "Carbonatacion";
                        break;
                    case "BX-":
                        VariableYEnv = "A-L7-1";
                        VariableYName = "Brix";
                        break;
                    case "CN-":
                        VariableYEnv = "A-L7-3";
                        VariableYName = "Contenido Neto";
                        break;
                }
            }
            else
            {
                VariableYEnv = variableY;
            }

            var dataResultY = getDataQuality.getResultsByVarX(
                token, _settings.QueryResultVarY_X, _settings.Company,
                line, startDate, endDate, VariableYEnv);
            await Task.WhenAll(dataResultY);

            var scatterDataY = new List<object>();
            if (dataResultY.Result.result != null)
            {
                scatterDataY = dataResultY.Result.result
                    .Where(r => r.resultValue != null && r.executionDate != null)
                    .OrderBy(r => r.executionDate)
                    .Select(r => new { Time = r.executionDate.ToString("dd-MM-yy HH:mm"), Value = r.resultValue ?? 0 })
                    .ToList<object>();
                // 📌 Obtener los valores min y max de la variable Y
                ViewBag.MinThreshold = dataResultY.Result.result.Min(r => r.minTolerance ?? r.resultValue ?? 0);
                ViewBag.MaxThreshold = dataResultY.Result.result.Max(r => r.maxTolerance ?? r.resultValue ?? 0);
                
            }

            ViewBag.ScatterDataY = scatterDataY;
            ViewBag.VariableY = VariableYEnv;
            ViewBag.VariableYName = VariableYName;
            ViewBag.VariablesXData = variablesXData;
            ViewBag.VariableY = variableY;
            ViewBag.Dates = scatterDataY.Select(d => ((dynamic)d).Time).ToList();

            return View("DetailResult");  // 🔥 Redirigir a la nueva vista
        }



        public async Task<JsonResult> ObtenerProductos(string lineaId, DateTime fechaInicial, DateTime fechaFinal)
        {
            string token = HttpContext.Session.GetString("AuthToken"); // Obtener el token de la sesión

            if (string.IsNullOrEmpty(token))
            {
                return Json(new { error = "Token no disponible" });
            }
            else
            {

           
                var dataResultP = getDataQuality.getProductsByLine(
                        token.ToString(),
                        _settings.QuerySKUs,
                        _settings.Company,
                        lineaId,
                        fechaInicial,
                        fechaFinal);
                await Task.WhenAll(dataResultP);

                var resultado = Json(dataResultP.Result.result);
                return resultado;

            }

        }


        [HttpGet]
        [HttpGet]
        public async Task<JsonResult> ObtenerVarY(string sku, DateTime startDate, DateTime endDate, string line)
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { value = Array.Empty<object>(), error = "Token no disponible" });

            // 1) Llamada al WS que devuelve FILAS CRUDAS (resultValue, min/maxTolerance, executionDate, etc.)
            //    ⚠️ Esta llamada usa getVarYRows (ver la clase en el punto 2).
            var dataTask = getDataQuality.getVarYRows(
                token,
                _settings.QueryVarY,   // tu endpoint/URL configurada para este query
                _settings.Company,
                sku,
                startDate,
                endDate,
                line
            );

            await Task.WhenAll(dataTask);

            // Materializamos a lista para poder usar .Count, .ToList(), etc.
            var rows = (dataTask.Result?.result ?? Enumerable.Empty<YRawRow>()).ToList();
            if (rows.Count == 0)
                return Json(new { value = Array.Empty<object>() });

            // 2) Construir el objeto que espera tu UI (cards + spark)
            int totalDays = (int)(endDate.Date - startDate.Date).TotalDays + 1;

            // ... arriba no cambies nada

            var items = rows
                .GroupBy(r => new {
                    Op = (r.controlOperation ?? "").Trim().ToUpper(),
                    Name = (r.controlOperationName ?? "").Trim()
                })
                .Select(g =>
                {
                    var ordered = g.OrderBy(r => r.executionDate).ToList();
                    int tests = ordered.Count;

                    // Último registro (para "last" y tolerancias)
                    DateTime? lastTs = null; double? lastVal = null;
                    double? lsl = null, usl = null;
                    if (tests > 0)
                    {
                        var last = ordered[^1];
                        lastTs = last.executionDate;
                        lastVal = last.resultValue;
                        lsl = last.minTolerance;
                        usl = last.maxTolerance;
                    }

                    // Cobertura y OOS
                    int coverageDays = ordered.Select(r => r.executionDate.Date).Distinct().Count();
                    int oos = ordered.Count(r =>
                        r.resultValue.HasValue &&
                        r.minTolerance.HasValue &&
                        r.maxTolerance.HasValue &&
                        (r.resultValue.Value < r.minTolerance.Value ||
                         r.resultValue.Value > r.maxTolerance.Value));

                    // Media
                    double? mean = null;
                    var valsAll = ordered.Where(r => r.resultValue.HasValue).Select(r => r.resultValue!.Value).ToList();
                    if (valsAll.Count > 0) mean = valsAll.Average();

                    // 🔹 SPARK: ÚLTIMOS 10 CONTROLES (no por día)
                    var last10Vals = ordered
                        .Where(r => r.resultValue.HasValue)
                        .OrderByDescending(r => r.executionDate)
                        .Take(10)
                        .Select(r => r.resultValue!.Value)
                        .Reverse()             // para que se dibuje cronológicamente izquierda→derecha
                        .ToList();

                    return new
                    {
                        codigo = g.Key.Op,
                        nombre = $"[{tests}] {g.Key.Name}",
                        tests = tests,
                        cov = $"{coverageDays}/{totalDays} días",
                        last = lastTs.HasValue
                                    ? $"{lastTs:dd-MM-yy HH:mm} ({(lastVal.HasValue ? lastVal.Value.ToString("0.##") : "—")})"
                                    : "—",
                        oos = oos,
                        mean = mean,
                        spark = last10Vals,   // 👈 ahora es una lista simple con los últimos 10
                        lsl = lsl,          // 👈 límites (si existen)
                        usl = usl
                    };
                })
                .OrderByDescending(x => x.tests)
                .ToList();

            return Json(new { value = items });

        }





        [HttpGet]
        public async Task<JsonResult> ObtenerVarX(
    string sku, string varY, DateTime fechaInicial, DateTime fechaFinal, string lineaId)
        {
            string token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { error = "Token no disponible" });

            // 1) Traer lista de X ligadas a la Y y QUITAR DUPLICADOS por código
            var prefix = (varY?.Length >= 3 ? varY.Substring(0, 3) : varY ?? "") + "%";
            var opsTask = getDataQuality.getVarXByvarY(
                token, _settings.QueryVarX, _settings.Company,
                sku, prefix, fechaInicial, fechaFinal, lineaId);

            await Task.WhenAll(opsTask);

            var opsRaw = opsTask.Result?.result ?? new List<result_varY>();
            var ops = opsRaw
                .Where(o => !string.IsNullOrWhiteSpace(o.controlOperation))
                .GroupBy(o => o.controlOperation)           // ← DEDUP por código
                .Select(g => g.First())
                .ToList();

            if (ops.Count == 0)
                return Json(new { value = Array.Empty<object>() });

            // 2) Para cada X única, pedir resultados en paralelo
            var tasks = new List<Task<result_Q_Resultados>>();
            foreach (var op in ops)
            {
                tasks.Add(getDataQuality.getResultsByVarX(
                    token, _settings.QueryResultVarY_X, _settings.Company,
                    lineaId, fechaInicial, fechaFinal, op.controlOperation));
            }

            await Task.WhenAll(tasks);

            // 3) Construir resumen por X (agrupando muestreos por DÍA)
            var items = new List<object>();

            foreach (var t in tasks)
            {
                var rows = t.Result?.result ?? new List<result_Resultados>();
                if (rows.Count == 0) continue;

                var ordered = rows
                    .Where(r => r.executionDate != null)
                    .OrderBy(r => r.executionDate)
                    .ToList();

                // Último registro real
                var last = ordered[^1];
                DateTime? lastTs = last.executionDate;
                double? lastVal = last.resultValue;

                // LSL / USL (primer no nulo)
                double? lsl = ordered.Select(r => r.minTolerance).FirstOrDefault(v => v.HasValue);
                double? usl = ordered.Select(r => r.maxTolerance).FirstOrDefault(v => v.HasValue);

                // CKlists: contar DÍAS con dato (no cada muestra)
                int tests = ordered
                    .Where(r => r.resultValue.HasValue)
                    .Select(r => r.executionDate.Date)
                    .Distinct()
                    .Count();

                // Spark: promedio por DÍA en [fechaInicial, fechaFinal], reducido a 10 puntos
                var spark = BuildSparkX(ordered, fechaInicial, fechaFinal, 10);

                // Nombre/código desde la fila
                string value = last.controlOperation ?? "";
                string name = last.controlOperationName ?? value;

                items.Add(new
                {
                    value,
                    name,
                    tests, // ← ahora son días (CKlists)
                    last = lastTs.HasValue
                            ? $"{lastTs:dd-MM-yy HH:mm} ({(lastVal.HasValue ? lastVal.Value.ToString("0.##") : "—")})"
                            : "—",
                    lsl,
                    usl,
                    spark
                });
            }

            // (Opcional) Ordena por nombre para que no “salten”
            items = items.OrderBy(i => (string)i.GetType().GetProperty("name")!.GetValue(i)!).ToList();

            return Json(new { value = items });
        }

        // Helpers SIN cambios
        private static List<double> BuildSparkX(IEnumerable<result_Resultados> rows, DateTime f1, DateTime f2, int points)
        {
            var byDay = rows
                .Where(r => r.resultValue.HasValue)
                .Where(r => r.executionDate >= f1 && r.executionDate <= f2)
                .GroupBy(r => r.executionDate.Date)                   // ← prom. POR DÍA
                .OrderBy(g => g.Key)
                .Select(g => g.Average(x => x.resultValue!.Value))
                .ToList();

            if (byDay.Count <= points) return byDay;
            return Downsample(byDay, points);
        }


        // reduce un array largo a 'target' puntos promediando ventanas
        private static List<double> Downsample(IList<double> src, int target)
        {
            if (src == null || src.Count == 0) return new();
            if (src.Count <= target) return src.ToList();

            var step = (double)src.Count / target;
            var outp = new List<double>(target);

            for (int i = 0; i < target; i++)
            {
                int a = (int)Math.Round(i * step);
                int b = (int)Math.Round((i + 1) * step);
                a = Math.Clamp(a, 0, src.Count - 1);
                b = Math.Clamp(b, 0, src.Count);
                if (b <= a) b = Math.Min(a + 1, src.Count);
                var slice = src.Skip(a).Take(b - a).ToList();
                outp.Add(slice.Average());
            }
            return outp;
        }



        public IActionResult ConfigDashboard()
        {
            // Simulando datos para los selectores


            return View();
        }

        public IActionResult Dashboard()
        {
            // Simulando datos para los selectores


            return View();
        }

        public IActionResult DetailResult()
        {
            return View("DetailResult"); // 📌 Sin ruta completa, solo el nombre
        }


       
    }

}
