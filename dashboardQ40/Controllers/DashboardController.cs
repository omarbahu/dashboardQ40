using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;
using static dashboardQ40.Models.Models;
using System.Data;

namespace dashboardQ40.Controllers
{
    public class DashboardController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;
        private readonly Dictionary<string, string> _variablesY;
        private readonly ILogger<DashboardController> _logger;
        private readonly IConfiguration _configuration;
        private string ConnStr => _configuration.GetConnectionString("CaptorConnection");


        public DashboardController(IOptions<WebServiceSettings> settings, AuthService authService, IConfiguration configuration, ILogger<DashboardController> logger)
        {
            _settings = settings.Value;
            _authService = authService;
            _logger = logger;
            _configuration = configuration;

            // 📌 Cargar la configuración manualmente
            _variablesY = configuration.GetSection("VariablesY").Get<Dictionary<string, string>>() ?? new Dictionary<string, string>();

       
        }
        public async Task<IActionResult> IndexAsync()
        {

            var token = await _authService.ObtenerTokenCaptor(_settings.Company);
            if (token != null)
            {
                HttpContext.Session.SetString("AuthToken", token.access_token); // Guardar en sesión
            }

            var ListCompanies = new List<result_companies>();
            var companies = new List<CompanyOption>();

            if (token != null)
            {


                Task<result_Q_Companies> dataResultComp = getDataQuality.getCompanies(
                        token.access_token.ToString(),
                        _settings.BaseUrl + _settings.QueryCompany,
                        _settings.Company,
                        _settings.trazalog);
                await Task.WhenAll(dataResultComp);

                if (dataResultComp.Result.result != null)
                {
                    foreach (var item in dataResultComp.Result.result)
                    {
                        CultureInfo ci;
                        RegionInfo ri;
                        try
                        {
                            ci = new CultureInfo(item.culture);   // p.ej. "es-MX"
                            ri = new RegionInfo(ci.Name);         // p.ej. "MX"
                        }
                        catch
                        {
                            ci = CultureInfo.InvariantCulture;
                            ri = new RegionInfo("US");            // fallback
                        }

                        companies.Add(new CompanyOption
                        {
                            Company = item.company,
                            CompanyName = item.companyName,
                            Culture = ci.Name,
                            CountryCode = ri.TwoLetterISORegionName
                        });
                    }

                }

            }
            var countries = companies
                  .GroupBy(c => c.CountryCode)
                  .Select(g =>
                  {
                      var r = new RegionInfo(g.Key); // admite "MX","US","ES"
                      return new { Code = g.Key, Name = r.NativeName }; // "México", "Estados Unidos"
                  })
                  .OrderBy(x => x.Name)
                  .ToList();

            // Simulando datos para los selectores



            ViewBag.Companies = companies;                 // lista completa
            ViewBag.Countries = countries;                 // países únicos
            ViewBag.CompaniesJson = JsonConvert.SerializeObject(companies);

            ViewBag.produccion = _settings.Produccion;

            return View();
        }


        [HttpGet("ObtenerLineas")]
        public async Task<IActionResult> ObtenerLineas(string company)
        {
            string token;
            if (company != _settings.Company) // significa que es una compañia diferente a la base y bamos por el token de la compañia
            {                
                var result = await _authService.ObtenerTokenCaptor(company);
                if (result != null)
                {
                    HttpContext.Session.SetString("AuthToken", result.access_token); // Guardar en sesión
                }
                token = result.access_token;  // Usamos el string del token
            }
            else
            {
                token = HttpContext.Session.GetString("AuthToken");
            }    
            
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(company))
                return Json(Array.Empty<object>());

            // Llama a tu servicio; ajusta nombres de método y settings
            var resp = await getDataQuality.getLinesByCompany(
                token,
                _settings.BaseUrl + _settings.QueryLineas + company, // tu query
                company,                  // si lo pides, o quítalo
                _settings.trazalog                             // filtro de company
            );

            var list = resp?.result?.Select(w => new {
                workplace = w.workplace,           // id
                workplaceName = w.workplaceName    // nombre
            }) ?? Enumerable.Empty<object>();

            return Json(list);
        }

        private async Task CargarCombosAsync()
        {
            var token = await _authService.ObtenerTokenCaptor(_settings.Company);
            if (token != null)
                HttpContext.Session.SetString("AuthToken", token.access_token);

            var companies = new List<CompanyOption>();
            if (token != null)
            {
                var dataResultComp = await getDataQuality.getCompanies(
                    token.access_token.ToString(),
                    _settings.BaseUrl + _settings.QueryCompany,
                    _settings.Company,
                    _settings.trazalog);

                foreach (var item in dataResultComp.result ?? Enumerable.Empty<result_companies>())
                {
                    CultureInfo ci; RegionInfo ri;
                    try { ci = new CultureInfo(item.culture); ri = new RegionInfo(ci.Name); }
                    catch { ci = CultureInfo.InvariantCulture; ri = new RegionInfo("US"); }

                    companies.Add(new CompanyOption
                    {
                        Company = item.company,
                        CompanyName = item.companyName,
                        Culture = ci.Name,
                        CountryCode = ri.TwoLetterISORegionName
                    });
                }
            }

            var countries = companies.GroupBy(c => c.CountryCode)
                .Select(g => new { Code = g.Key, Name = new RegionInfo(g.Key).NativeName })
                .OrderBy(x => x.Name).ToList();

            ViewBag.Companies = companies;
            ViewBag.Countries = countries;
            ViewBag.CompaniesJson = JsonConvert.SerializeObject(companies);
            ViewBag.produccion = _settings.Produccion;
        }


        [HttpGet]
        public async Task<IActionResult> Resumen()
        {
            await CargarCombosAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSelection(
     string line,
     DateTime startDate,
     DateTime endDate,
     string product,
     string variableY,
     string planta,
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
                        _settings.BaseUrl + _settings.QueryResultVarY_X + planta,
                        planta,
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

                VariableYEnv = variableY;


            // 📌 Obtener los datos de la Variable Y
            var dataResultY = getDataQuality.getResultsByVarX(
                token.ToString(),
                _settings.BaseUrl + _settings.QueryResultVarY_X + planta,
                planta,
                line, startDate, endDate, VariableYEnv);
            await Task.WhenAll(dataResultY);
            if (string.IsNullOrEmpty(VariableYName))
            {
                VariableYName = dataResultY.Result.result?
                    .FirstOrDefault()?.controlOperationName
                    ?? variableY; // si no hay nombre, al menos el código
            }
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


        // Máx. puntos que mandaremos al front por serie
        const int MAX_POINTS_PER_SERIES = 2000;

        [HttpPost]
        public async Task<IActionResult> SubmitSelectionDetail(
            string line,
            DateTime startDate,
            DateTime endDate,
            string product,
            string variableY,
            string planta,
            List<string> variablesX)
        {
            string token = HttpContext.Session.GetString("AuthToken");
            var random = new Random();

            var variablesXData = new Dictionary<string, List<double>>();
            var variablesXNames = new Dictionary<string, string>();   // código -> nombre amigable

            // ========= 1) VARIABLES X =========
            if (variablesX != null && variablesX.Count > 0)
            {
                foreach (var variableX in variablesX)
                {
                    var dataResultP = await getDataQuality.getResultsByVarX(
                        token,
                        _settings.BaseUrl + _settings.QueryResultVarY_X + planta,
                        planta,
                        line,
                        startDate,
                        endDate,
                        variableX);

                    if (dataResultP?.result == null || !dataResultP.result.Any())
                        continue;

                    // Ordenamos por fecha por si hace falta
                    var orderedX = dataResultP.result
                        .Where(r => r.resultValue != null)
                        .OrderBy(r => r.executionDate)
                        .ToList();

                    var values = orderedX
                        .Select(r => Convert.ToDouble(r.resultValue))
                        .ToList();

                    // Downsampling para X (mismas reglas que Y)
                    if (values.Count > MAX_POINTS_PER_SERIES)
                    {
                        var stepX = (int)Math.Ceiling(values.Count / (double)MAX_POINTS_PER_SERIES);
                        values = values
                            .Where((v, idx) => idx % stepX == 0)
                            .ToList();
                    }

                    if (!values.Any())
                    {
                        // Fallback demo
                        values = Enumerable.Range(0, 30)
                            .Select(_ => (double)random.Next(1, 100))
                            .ToList();
                    }

                    variablesXData[variableX] = values;

                    // Nombre amigable de la operación (si viene en el WS)
                    var opName = dataResultP.result
                        .Select(r => r.controlOperationName)
                        .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

                    variablesXNames[variableX] = opName ?? variableX;
                }
            }

            // ========= 2) VARIABLE Y =========
            var VariableYEnv = variableY; // de momento mismo código
            var VariableYName = variableY;

            var dataResultY = await getDataQuality.getResultsByVarX(
                token,
                _settings.BaseUrl + _settings.QueryResultVarY_X + planta,
                planta,
                line,
                startDate,
                endDate,
                VariableYEnv);

            var scatterDataY = new List<object>();
            var datesForY = new List<string>();

            if (dataResultY?.result != null && dataResultY.result.Any())
            {
                var all = dataResultY.result;

                // 📌 Umbrales usando TODOS los puntos originales de Y
                ViewBag.MinThreshold = all.Min(r => r.minTolerance ?? r.resultValue ?? 0);
                ViewBag.MaxThreshold = all.Max(r => r.maxTolerance ?? r.resultValue ?? 0);

                // Ordenamos por fecha
                var ordered = all
                    .Where(r => r.resultValue != null)
                    .OrderBy(r => r.executionDate)
                    .ToList();

                // 📉 Downsampling (si hay muchos puntos)
                if (ordered.Count > MAX_POINTS_PER_SERIES)
                {
                    var step = (int)Math.Ceiling(ordered.Count / (double)MAX_POINTS_PER_SERIES);
                    ordered = ordered
                        .Where((r, idx) => idx % step == 0)
                        .ToList();
                }

                // ⚠️ Aquí ya NO usamos "??" con DateTime → asumimos que executionDate NO es nullable.
                scatterDataY = ordered
                    .Select(r => new
                    {
                        Time = r.executionDate.ToString("dd-MM-yy HH:mm"),
                        Value = Convert.ToDouble(r.resultValue ?? 0)
                    })
                    .Cast<object>()
                    .ToList();

                datesForY = ordered
                    .Select(r => r.executionDate.ToString("dd-MM-yy HH:mm"))
                    .ToList();
            }
            else
            {
                ViewBag.MinThreshold = 0d;
                ViewBag.MaxThreshold = 0d;
            }

            // ========= 3) ViewBags hacia la vista =========
            ViewBag.ScatterDataY = scatterDataY;
            ViewBag.VariableY = VariableYEnv;
            ViewBag.VariableYName = VariableYName;
            ViewBag.VariablesXData = variablesXData;
            ViewBag.VariableXNames = variablesXNames;   // <<< nombres amigables de X
            ViewBag.Dates = datesForY;

            return View("DetailResult");
        }





        public async Task<JsonResult> ObtenerProductos(string lineaId, DateTime fechaInicial, DateTime fechaFinal, string planta)
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
                        _settings.BaseUrl + _settings.QuerySKUs + planta,
                        planta,
                        lineaId,
                        fechaInicial,
                        fechaFinal);
                await Task.WhenAll(dataResultP);

                var resultado = Json(dataResultP.Result.result);
                return resultado;

            }

        }



        [HttpGet]
        public async Task<JsonResult> ObtenerVarY(string sku, DateTime startDate, DateTime endDate, string line, string planta)
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { value = Array.Empty<object>(), error = "Token no disponible" });

            // 1) Llamada al WS que devuelve FILAS CRUDAS (resultValue, min/maxTolerance, executionDate, etc.)
            //    ⚠️ Esta llamada usa getVarYRows (ver la clase en el punto 2).
            var dataTask = getDataQuality.getVarYRows(
                token,
                _settings.BaseUrl + _settings.QueryVarY + planta,   // tu endpoint/URL configurada para este query
                planta,
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
        public async Task<JsonResult> ObtenerAllVarCertf(string sku, DateTime startDate, DateTime endDate, string line, string planta)
        {
            var token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { value = Array.Empty<object>(), error = "Token no disponible" });

            // 1) Llamada al WS que devuelve FILAS CRUDAS (resultValue, min/maxTolerance, executionDate, etc.)
            //    ⚠️ Esta llamada usa getVarYRows (ver la clase en el punto 2).
            var dataTask = getDataQuality.getVarYRows(
                token,
                _settings.BaseUrl + _settings.QueryVarAllCert + planta,   // tu endpoint/URL configurada para este query
                planta,
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
    string sku, string varY, DateTime fechaInicial, DateTime fechaFinal, string lineaId, string planta)
        {
            string token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return Json(new { error = "Token no disponible" });

            // 1) Traer lista de X ligadas a la Y y QUITAR DUPLICADOS por código
            var prefix = (varY?.Length >= 3 ? varY.Substring(0, 3) : varY ?? "") + "%";
            var opsTask = getDataQuality.getVarXByvarY(
                token, _settings.BaseUrl + _settings.QueryVarX + planta, planta,
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
                    token, _settings.BaseUrl + _settings.QueryResultVarY_X + planta, planta,
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

        [HttpGet]
        public async Task<IActionResult> ResumenCPKs(
     DateTime startDate, DateTime endDate, string planta, int tzOffset = 0)
        {
            if (startDate >= endDate)
            {
                ViewBag.ErrorMessage = "La fecha de fin debe ser mayor a la fecha de inicio.";
                await CargarCombosAsync();
                return View("Resumen", new List<CapabilityRow>());
            }

            // Ajuste a UTC
            var fromUtc = startDate.AddMinutes(-tzOffset);
            var toUtc = endDate.AddMinutes(-tzOffset);

            // <<< Reponer selección >>>
            await CargarCombosAsync();
            ViewBag.PlantaSelected = planta;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            var connStr = _configuration.GetConnectionString("CaptorConnection");
            var rows = CpkService.GetResumenCpk(planta, fromUtc, toUtc, connStr);

            return View("Resumen", rows);
        }

        [HttpGet]
        public async Task<IActionResult> CertificadoCalidad()
        {
            await CargarCombosAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GenerarCertificado(CertificadoRequestModel model)
        {
            string token = HttpContext.Session.GetString("AuthToken");
            if (string.IsNullOrEmpty(token))
                return BadRequest("Token no disponible.");

            // Rango de 1 día (esto tú ya lo usabas así)
            var start = model.Fecha.Date;
            var end = start.AddDays(1);

            var listaVariables = new List<CertificadoCaracteristicaDto>();

            if (model.VariablesY != null && model.VariablesY.Count > 0)
            {
                foreach (var codigo in model.VariablesY)
                {
                    // OJO: aquí sólo usamos LO QUE YA EXISTE:
                    // getDataQuality.getResultsByVarX  +  Helpers.BuildCertificadoRow

                    var dataResult = await getDataQuality.getResultsByVarX(
                        token,
                        _settings.BaseUrl + _settings.QueryResultVarY_X + model.Planta,
                        model.Planta,
                        model.Line,
                        start,
                        end,
                        codigo);

                    var rows = dataResult?.result?.ToList() ?? new List<result_Resultados>();

                    // Este helper es el que ya tenías para armar la fila del certificado
                    var rowStat = Helpers.BuildCertificadoRow(codigo, rows);

                    if (rowStat != null)
                        listaVariables.Add(rowStat);
                }
            }

            var vm = new CertificadoCalidadViewModel
            {
                CompanyName = "",
                PlantName = model.PlantaSuministro ?? model.Planta,
                Country = model.Pais,
                City = "",
                Address = "",
                Comentario = model.PlantaSuministro,
                FechaImpresion = DateTime.Now,
                NombreParte = model.Sku,
                Linea = model.LineaTexto ?? model.Line,
                Turno = model.Turno,
                CodigoProduccion = model.CodigoLote,
                Lote = model.CodigoLote,
                TamanoLoteCajas = Helpers.ParseTamanoLote(model.TamanoLoteTexto),
                Analista = model.Analista,
                AnalistasProceso = model.AnalistasProceso,
                SupervisorCalidad = model.SupervisorCalidad,
                JefeCalidad = model.JefeCalidad,
                Sabor = model.Sabor,
                Apariencia = model.Apariencia,
                PlantaSuministro = model.PlantaSuministro,
                TamanoLoteTexto = model.TamanoLoteTexto,
                Caracteristicas = listaVariables
            };

            return View("CertificadoPreview", vm);
        }



        private CertificadoCaracteristicaDto? BuildCaracteristicaDesdeXR(
    string planta,
    DateTime fecha,
    string line,
    string codigoVarY)
        {
            var f1 = fecha.Date;
            var f2 = f1.AddDays(1);

            // 1) Traer lecturas base con la misma lógica que XR
            var baseDt = XRchartsService.GetXRBaseRows(
                planta,
                f1,
                f2,
                ConnStr,
                line,
                reference: null,
                controlOperation: codigoVarY
            );

            if (baseDt == null || baseDt.Rows.Count == 0)
                return null;

            // 2) Calcular capability (Cp, Cpk, etc.) igual que XR
            var capDt = XRchartsService.BuildCapability(baseDt);
            if (capDt == null || capDt.Rows.Count == 0)
                return null;

            var capRow = capDt.Rows[0];   // viene una por operación

            // --- Campos de capability (los mismos que usas en renderXRCapTable) ---
            var nombre = capRow.Field<string>("controlOperationName")
                         ?? capRow.Field<string>("controlOperation")
                         ?? codigoVarY;

            int? nPoints = capRow.Field<int?>("nPoints");
            decimal? meanAll = capRow.Field<decimal?>("meanAll");
            decimal? sigmaWithin = capRow.Field<decimal?>("sigmaWithin");
            decimal? sigmaOverall = capRow.Field<decimal?>("sigmaOverall");
            decimal? lsl = capRow.Field<decimal?>("LSL");
            decimal? usl = capRow.Field<decimal?>("USL");
            decimal? cpk = capRow.Field<decimal?>("Cpk");
            // si luego quieres Cp/Pp/Ppk también los puedes tomar de aquí

            // --- Cálculo de % bajo LEI y % sobre LES a partir de baseDt ---
            var valores = baseDt
                .AsEnumerable()
                .Select(r => new
                {
                    Value = r.Field<decimal?>("resultValue"),
                    LSL = r.Field<decimal?>("LSL"),
                    USL = r.Field<decimal?>("USL")
                })
                .Where(x => x.Value.HasValue)
                .ToList();

            int n = valores.Count;
            decimal? pctBajo = null;
            decimal? pctSobre = null;

            if (n > 0)
            {
                int bajo = valores.Count(v => v.LSL.HasValue && v.Value.Value < v.LSL.Value);
                int sobre = valores.Count(v => v.USL.HasValue && v.Value.Value > v.USL.Value);

                pctBajo = (decimal)bajo * 100m / n;
                pctSobre = (decimal)sobre * 100m / n;
            }

            // OJO: aquí asumo propiedades de tu DTO,
            // sólo mapea a los nombres reales de CertificadoCaracteristicaDto
            return new CertificadoCaracteristicaDto
            {
                Nombre = nombre,
                Muestras = nPoints,
                LEI = lsl,
                LES = usl,
                Media = meanAll,
                // Usa la que prefieras: Within o Overall
                Sigma = sigmaWithin ?? sigmaOverall,
                PorcBajoLEI = pctBajo,
                PorcSobreLES = pctSobre,
                Cpk = cpk
            };
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
