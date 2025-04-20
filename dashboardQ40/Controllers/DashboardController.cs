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

            ViewBag.produccion = _settings.produccion;

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
            if (_settings.produccion == "0")
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
            if (_settings.produccion == "0")
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


        public async Task<JsonResult> ObtenerVarY(string sku)
        {
            string token = HttpContext.Session.GetString("AuthToken"); // Obtener el token de la sesión

            var ListVariablesY = new List<result_varY>();

            if (string.IsNullOrEmpty(token))
            {
                return Json(new { error = "Token no disponible" });
            }
            else
            {
                var dataResultP = getDataQuality.getVarYBysku(
                        token.ToString(),
                        _settings.QueryVarY,
                        _settings.Company,
                        sku);
                await Task.WhenAll(dataResultP);

                if (dataResultP.Result.result != null)
                {
                    foreach (var item in dataResultP.Result.result)
                    {
                        string controlOp = item.controlOperation?.Trim().ToUpper(); // Limpiar espacios y mayúsculas
                        string controlOpName = item.controlOperationName?.Trim();

                    
                        var varY = new result_varY
                        {
                            controlOperation = controlOp,
                            controlOperationName = controlOpName
                        };
                        ListVariablesY.Add(varY);
                    }
                }

                string json = JsonSerializer.Serialize(ListVariablesY, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                _logger.LogInformation($"json de variables y: {json}");

                bool esProduccion = _settings.produccion == "1"; // Lee desde appsettings

                var variablesFiltradas = ListVariablesY
                    .Where(d => esProduccion || _variablesY.Keys.Any(prefijo => d.controlOperation.StartsWith(prefijo.Trim().ToUpper())))
                    .Select(d =>
                    {
                        if (esProduccion)
                        {
                            return new
                            {
                                Codigo = d.controlOperation,
                                Nombre = d.controlOperationName
                            };
                        }
                        else
                        {
                            var kvp = _variablesY.FirstOrDefault(p => d.controlOperation.StartsWith(p.Key));
                            return new
                            {
                                Codigo = kvp.Key,
                                Nombre = kvp.Value
                            };
                        }
                    })
                    .Distinct()
                    .ToList();

                string json2 = JsonSerializer.Serialize(variablesFiltradas, new JsonSerializerOptions { WriteIndented = true });
                
                _logger.LogInformation($"json de variablesfiltradas y: {json2}");

                _logger.LogInformation($"Total de variables Y en el diccionario: {_variablesY.Count}");
                _logger.LogInformation($"Total de variables encontradas en la BD: {ListVariablesY.Count}");
                _logger.LogInformation($"Total de variables filtradas para el dropdown: {variablesFiltradas.Count}");

                return Json(new { value = variablesFiltradas });

            }
        }

        public async Task<JsonResult> ObtenerVarX(string sku, string varY)
        {
            string token = HttpContext.Session.GetString("AuthToken"); // Obtener el token de la sesión

            var ListVariablesY = new List<result_varY>();

            if (string.IsNullOrEmpty(token))
            {
                return Json(new { error = "Token no disponible" });
            }
            else
            {
                var dataResultP = getDataQuality.getVarXByvarY(
                        token.ToString(),
                        _settings.QueryVarX,
                        _settings.Company,
                        sku,varY);
                await Task.WhenAll(dataResultP);

                var resultado = Json(dataResultP.Result.result);
                return resultado;

            }
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
