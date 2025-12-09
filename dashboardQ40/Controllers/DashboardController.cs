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
            try
            {
                var token = await _authService.ObtenerTokenCaptor(_settings.Company);
                if (token != null)
                {
                    HttpContext.Session.SetString("AuthToken", token.access_token);
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
                                ci = new CultureInfo(item.culture);
                                ri = new RegionInfo(ci.Name);
                            }
                            catch
                            {
                                ci = CultureInfo.InvariantCulture;
                                ri = new RegionInfo("US");
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
                          var r = new RegionInfo(g.Key);
                          return new { Code = g.Key, Name = r.NativeName };
                      })
                      .OrderBy(x => x.Name)
                      .ToList();

                ViewBag.Companies = companies;
                ViewBag.Countries = countries;
                ViewBag.CompaniesJson = JsonConvert.SerializeObject(companies);
                ViewBag.produccion = _settings.Produccion;

                return View();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in IndexAsync");
                ViewBag.ErrorMessage = "Unable to load company data. Please try again.";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in IndexAsync");
                ViewBag.ErrorMessage = "An unexpected error occurred.";
                return View();
            }
        }



        [HttpGet("ObtenerLineas")]
        public async Task<IActionResult> ObtenerLineas(string company)
        {
            try
            {
                string token;
                if (company != _settings.Company)
                {
                    var result = await _authService.ObtenerTokenCaptor(company);
                    if (result == null)
                    {
                        _logger.LogWarning("Failed to obtain token for company: {Company}", company);
                        return Unauthorized(new { error = "Authentication failed" });
                    }

                    HttpContext.Session.SetString("AuthToken", result.access_token);
                    token = result.access_token;
                }
                else
                {
                    token = HttpContext.Session.GetString("AuthToken");
                }

                if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(company))
                    return Json(Array.Empty<object>());

                var resp = await getDataQuality.getLinesByCompany(
                    token,
                    _settings.BaseUrl + _settings.QueryLineas + company,
                    company,
                    _settings.trazalog
                );

                var list = resp?.result?.Select(w => new {
                    workplace = w.workplace,
                    workplaceName = w.workplaceName
                }) ?? Enumerable.Empty<object>();

                return Json(list);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerLineas for company: {Company}", company);
                return StatusCode(503, new { error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerLineas for company: {Company}", company);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private async Task CargarCombosAsync()
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading combos in CargarCombosAsync");
                // No hacemos throw porque este método es llamado desde otros con try-catch
                ViewBag.Companies = new List<CompanyOption>();
                ViewBag.Countries = new List<object>();
                ViewBag.CompaniesJson = "[]";
            }
        }



        [HttpGet]
        public async Task<IActionResult> Resumen()
        {
            try
            {
                await CargarCombosAsync();
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Resumen");
                return View();
            }
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
            try
            {
                string token = HttpContext.Session.GetString("AuthToken");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session");
                    return Unauthorized(new { error = "Session expired" });
                }

                var result_Resultados = new List<result_Resultados>();
                var random = new Random();
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
                            string nombreVariableX = dataResultP.Result.result.FirstOrDefault()?.controlOperationName ?? "Unknown";

                            var values = dataResultP.Result.result
                                .Where(r => r.resultValue != null)
                                .Select(r => Convert.ToDouble(r.resultValue))
                                .ToList();

                            if (!values.Any())
                            {
                                values = Enumerable.Range(0, 30).Select(_ => (double)random.Next(1, 100)).ToList();
                            }

                            variablesXData[nombreVariableX] = values;
                        }
                    }
                }

                var VariableYEnv = variableY;
                var VariableYName = "";

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
                        ?? variableY;
                }

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

                    minY = dataResultY.Result.result.Min(r => r.minTolerance ?? r.resultValue ?? 0);
                    maxY = dataResultY.Result.result.Max(r => r.maxTolerance ?? r.resultValue ?? 0);
                }

                ViewBag.ScatterDataY = scatterDataY;
                ViewBag.VariablesXData = variablesXData;
                ViewBag.VariableY = variableY;
                ViewBag.VariableYName = VariableYName;
                ViewBag.SelectedXVariables = variablesX;
                ViewBag.MinThreshold = minY;
                ViewBag.MaxThreshold = maxY;
                ViewBag.Dates = scatterDataY.Select(d => ((dynamic)d).Time).ToList();

                _logger.LogInformation("Variable Y: {VariableY}", variableY);
                _logger.LogInformation("Total Y data points: {Count}", scatterDataY.Count);
                _logger.LogInformation("Total X variables: {Count}", variablesXData.Keys.Count);

                foreach (var kvp in variablesXData)
                {
                    _logger.LogInformation("Variable X: {Name} - Data points: {Count}", kvp.Key, kvp.Value.Count);
                }

                if (scatterDataY.Count == 0)
                {
                    _logger.LogWarning("No data obtained for Variable Y: {VariableYEnv}", VariableYEnv);
                }

                return View("Result");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in SubmitSelection");
                return StatusCode(503, new { error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SubmitSelection");
                return StatusCode(500, new { error = "Internal server error" });
            }
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
            try
            {
                string token = HttpContext.Session.GetString("AuthToken");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session");
                    return Unauthorized(new { error = "Session expired" });
                }

                var random = new Random();
                var variablesXData = new Dictionary<string, List<double>>();
                var variablesXNames = new Dictionary<string, string>();

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

                        var orderedX = dataResultP.result
                            .Where(r => r.resultValue != null)
                            .OrderBy(r => r.executionDate)
                            .ToList();

                        var values = orderedX
                            .Select(r => Convert.ToDouble(r.resultValue))
                            .ToList();

                        if (values.Count > MAX_POINTS_PER_SERIES)
                        {
                            var stepX = (int)Math.Ceiling(values.Count / (double)MAX_POINTS_PER_SERIES);
                            values = values
                                .Where((v, idx) => idx % stepX == 0)
                                .ToList();
                        }

                        if (!values.Any())
                        {
                            values = Enumerable.Range(0, 30)
                                .Select(_ => (double)random.Next(1, 100))
                                .ToList();
                        }

                        variablesXData[variableX] = values;

                        var opName = dataResultP.result
                            .Select(r => r.controlOperationName)
                            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

                        variablesXNames[variableX] = opName ?? variableX;
                    }
                }

                var VariableYEnv = variableY;
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

                    ViewBag.MinThreshold = all.Min(r => r.minTolerance ?? r.resultValue ?? 0);
                    ViewBag.MaxThreshold = all.Max(r => r.maxTolerance ?? r.resultValue ?? 0);

                    var ordered = all
                        .Where(r => r.resultValue != null)
                        .OrderBy(r => r.executionDate)
                        .ToList();

                    if (ordered.Count > MAX_POINTS_PER_SERIES)
                    {
                        var step = (int)Math.Ceiling(ordered.Count / (double)MAX_POINTS_PER_SERIES);
                        ordered = ordered
                            .Where((r, idx) => idx % step == 0)
                            .ToList();
                    }

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

                ViewBag.ScatterDataY = scatterDataY;
                ViewBag.VariableY = VariableYEnv;
                ViewBag.VariableYName = VariableYName;
                ViewBag.VariablesXData = variablesXData;
                ViewBag.VariableXNames = variablesXNames;
                ViewBag.Dates = datesForY;

                return View("DetailResult");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in SubmitSelectionDetail");
                return StatusCode(503, new { error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SubmitSelectionDetail");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }



        public async Task<JsonResult> ObtenerProductos(string lineaId, DateTime fechaInicial, DateTime fechaFinal, string planta)
        {
            try
            {
                string token = HttpContext.Session.GetString("AuthToken");

                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session");
                    return Json(new { error = "Token not available" });
                }

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
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerProductos for line: {LineId}", lineaId);
                return Json(new { error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerProductos for line: {LineId}", lineaId);
                return Json(new { error = "Internal server error" });
            }
        }


        [HttpGet]
        public async Task<JsonResult> ObtenerVarY(string sku, DateTime startDate, DateTime endDate, string line, string planta)
        {
            try
            {
                var token = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session");
                    return Json(new { value = Array.Empty<object>(), error = "Token not available" });
                }

                var dataTask = getDataQuality.getVarYRows(
                    token,
                    _settings.BaseUrl + _settings.QueryVarY + planta,
                    planta,
                    sku,
                    startDate,
                    endDate,
                    line
                );

                await Task.WhenAll(dataTask);

                var rows = (dataTask.Result?.result ?? Enumerable.Empty<YRawRow>()).ToList();
                if (rows.Count == 0)
                    return Json(new { value = Array.Empty<object>() });

                int totalDays = (int)(endDate.Date - startDate.Date).TotalDays + 1;

                var items = rows
                    .GroupBy(r => new {
                        Op = (r.controlOperation ?? "").Trim().ToUpper(),
                        Name = (r.controlOperationName ?? "").Trim()
                    })
                    .Select(g =>
                    {
                        var ordered = g.OrderBy(r => r.executionDate).ToList();
                        int tests = ordered.Count;

                        DateTime? lastTs = null;
                        double? lastVal = null;
                        double? lsl = null, usl = null;
                        if (tests > 0)
                        {
                            var last = ordered[^1];
                            lastTs = last.executionDate;
                            lastVal = last.resultValue;
                            lsl = last.minTolerance;
                            usl = last.maxTolerance;
                        }

                        int coverageDays = ordered.Select(r => r.executionDate.Date).Distinct().Count();
                        int oos = ordered.Count(r =>
                            r.resultValue.HasValue &&
                            r.minTolerance.HasValue &&
                            r.maxTolerance.HasValue &&
                            (r.resultValue.Value < r.minTolerance.Value ||
                             r.resultValue.Value > r.maxTolerance.Value));

                        double? mean = null;
                        var valsAll = ordered.Where(r => r.resultValue.HasValue).Select(r => r.resultValue!.Value).ToList();
                        if (valsAll.Count > 0) mean = valsAll.Average();

                        var last10Vals = ordered
                            .Where(r => r.resultValue.HasValue)
                            .OrderByDescending(r => r.executionDate)
                            .Take(10)
                            .Select(r => r.resultValue!.Value)
                            .Reverse()
                            .ToList();

                        return new
                        {
                            codigo = g.Key.Op,
                            nombre = $"[{tests}] {g.Key.Name}",
                            tests = tests,
                            cov = $"{coverageDays}/{totalDays} days",
                            last = lastTs.HasValue
                                        ? $"{lastTs:dd-MM-yy HH:mm} ({(lastVal.HasValue ? lastVal.Value.ToString("0.##") : "—")})"
                                        : "—",
                            oos = oos,
                            mean = mean,
                            spark = last10Vals,
                            lsl = lsl,
                            usl = usl
                        };
                    })
                    .OrderByDescending(x => x.tests)
                    .ToList();

                return Json(new { value = items });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerVarY for sku: {Sku}", sku);
                return Json(new { value = Array.Empty<object>(), error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerVarY for sku: {Sku}", sku);
                return Json(new { value = Array.Empty<object>(), error = "Internal server error" });
            }
        }


        [HttpGet]
        public async Task<JsonResult> ObtenerAllVarCertf(string sku, DateTime startDate, DateTime endDate, string line, string planta)
        {
            try
            {
                var token = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session");
                    return Json(new { value = Array.Empty<object>(), error = "Token not available" });
                }

                var dataTask = getDataQuality.getVarYRows(
                    token,
                    _settings.BaseUrl + _settings.QueryVarAllCert + planta,
                    planta,
                    sku,
                    startDate,
                    endDate,
                    line
                );

                await Task.WhenAll(dataTask);

                var rows = (dataTask.Result?.result ?? Enumerable.Empty<YRawRow>()).ToList();
                if (rows.Count == 0)
                    return Json(new { value = Array.Empty<object>() });

                int totalDays = (int)(endDate.Date - startDate.Date).TotalDays + 1;

                var items = rows
                    .GroupBy(r => new {
                        Op = (r.controlOperation ?? "").Trim().ToUpper(),
                        Name = (r.controlOperationName ?? "").Trim()
                    })
                    .Select(g =>
                    {
                        var ordered = g.OrderBy(r => r.executionDate).ToList();
                        int tests = ordered.Count;

                        DateTime? lastTs = null;
                        double? lastVal = null;
                        double? lsl = null, usl = null;
                        if (tests > 0)
                        {
                            var last = ordered[^1];
                            lastTs = last.executionDate;
                            lastVal = last.resultValue;
                            lsl = last.minTolerance;
                            usl = last.maxTolerance;
                        }

                        int coverageDays = ordered.Select(r => r.executionDate.Date).Distinct().Count();
                        int oos = ordered.Count(r =>
                            r.resultValue.HasValue &&
                            r.minTolerance.HasValue &&
                            r.maxTolerance.HasValue &&
                            (r.resultValue.Value < r.minTolerance.Value ||
                             r.resultValue.Value > r.maxTolerance.Value));

                        double? mean = null;
                        var valsAll = ordered.Where(r => r.resultValue.HasValue).Select(r => r.resultValue!.Value).ToList();
                        if (valsAll.Count > 0) mean = valsAll.Average();

                        var last10Vals = ordered
                            .Where(r => r.resultValue.HasValue)
                            .OrderByDescending(r => r.executionDate)
                            .Take(10)
                            .Select(r => r.resultValue!.Value)
                            .Reverse()
                            .ToList();

                        return new
                        {
                            codigo = g.Key.Op,
                            nombre = $"[{tests}] {g.Key.Name}",
                            tests = tests,
                            cov = $"{coverageDays}/{totalDays} days",
                            last = lastTs.HasValue
                                        ? $"{lastTs:dd-MM-yy HH:mm} ({(lastVal.HasValue ? lastVal.Value.ToString("0.##") : "—")})"
                                        : "—",
                            oos = oos,
                            mean = mean,
                            spark = last10Vals,
                            lsl = lsl,
                            usl = usl
                        };
                    })
                    .OrderByDescending(x => x.tests)
                    .ToList();

                return Json(new { value = items });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerAllVarCertf for sku: {Sku}", sku);
                return Json(new { value = Array.Empty<object>(), error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerAllVarCertf for sku: {Sku}", sku);
                return Json(new { value = Array.Empty<object>(), error = "Internal server error" });
            }
        }


        [HttpGet]
        public async Task<JsonResult> ObtenerVarX(
     string sku, string varY, DateTime fechaInicial, DateTime fechaFinal, string lineaId, string planta)
        {
            try
            {
                string token = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session");
                    return Json(new { error = "Token not available" });
                }

                var prefix = (varY?.Length >= 3 ? varY.Substring(0, 3) : varY ?? "") + "%";
                var opsTask = getDataQuality.getVarXByvarY(
                    token, _settings.BaseUrl + _settings.QueryVarX + planta, planta,
                    sku, prefix, fechaInicial, fechaFinal, lineaId);

                await Task.WhenAll(opsTask);

                var opsRaw = opsTask.Result?.result ?? new List<result_varY>();
                var ops = opsRaw
                    .Where(o => !string.IsNullOrWhiteSpace(o.controlOperation))
                    .GroupBy(o => o.controlOperation)
                    .Select(g => g.First())
                    .ToList();

                if (ops.Count == 0)
                    return Json(new { value = Array.Empty<object>() });

                var tasks = new List<Task<result_Q_Resultados>>();
                foreach (var op in ops)
                {
                    tasks.Add(getDataQuality.getResultsByVarX(
                        token, _settings.BaseUrl + _settings.QueryResultVarY_X + planta, planta,
                        lineaId, fechaInicial, fechaFinal, op.controlOperation));
                }

                await Task.WhenAll(tasks);

                var items = new List<object>();

                foreach (var t in tasks)
                {
                    var rows = t.Result?.result ?? new List<result_Resultados>();
                    if (rows.Count == 0) continue;

                    var ordered = rows
                        .Where(r => r.executionDate != null)
                        .OrderBy(r => r.executionDate)
                        .ToList();

                    var last = ordered[^1];
                    DateTime? lastTs = last.executionDate;
                    double? lastVal = last.resultValue;

                    double? lsl = ordered.Select(r => r.minTolerance).FirstOrDefault(v => v.HasValue);
                    double? usl = ordered.Select(r => r.maxTolerance).FirstOrDefault(v => v.HasValue);

                    int tests = ordered
                        .Where(r => r.resultValue.HasValue)
                        .Select(r => r.executionDate.Date)
                        .Distinct()
                        .Count();

                    var spark = BuildSparkX(ordered, fechaInicial, fechaFinal, 10);

                    string value = last.controlOperation ?? "";
                    string name = last.controlOperationName ?? value;

                    items.Add(new
                    {
                        value,
                        name,
                        tests,
                        last = lastTs.HasValue
                                ? $"{lastTs:dd-MM-yy HH:mm} ({(lastVal.HasValue ? lastVal.Value.ToString("0.##") : "—")})"
                                : "—",
                        lsl,
                        usl,
                        spark
                    });
                }

                items = items.OrderBy(i => (string)i.GetType().GetProperty("name")!.GetValue(i)!).ToList();

                return Json(new { value = items });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in ObtenerVarX for sku: {Sku}", sku);
                return Json(new { error = "External service unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ObtenerVarX for sku: {Sku}", sku);
                return Json(new { error = "Internal server error" });
            }
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
            try
            {
                if (startDate >= endDate)
                {
                    ViewBag.ErrorMessage = "End date must be greater than start date.";
                    await CargarCombosAsync();
                    return View("Resumen", new List<CapabilityRow>());
                }

                var fromUtc = startDate.AddMinutes(-tzOffset);
                var toUtc = endDate.AddMinutes(-tzOffset);

                await CargarCombosAsync();
                ViewBag.PlantaSelected = planta;
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                var connStr = _configuration.GetConnectionString("CaptorConnection");
                var rows = CpkService.GetResumenCpk(planta, fromUtc, toUtc, connStr);

                return View("Resumen", rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ResumenCPKs for planta: {Planta}", planta);
                ViewBag.ErrorMessage = "An error occurred while generating the report.";
                await CargarCombosAsync();
                return View("Resumen", new List<CapabilityRow>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> CertificadoCalidad()
        {
            try
            {
                await CargarCombosAsync();
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CertificadoCalidad");
                return View();
            }
        }

        [HttpPost]
        public async Task<IActionResult> GenerarCertificado(CertificadoRequestModel model)
        {
            try
            {
                string token = HttpContext.Session.GetString("AuthToken");
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Token not found in session");
                    return BadRequest("Token not available.");
                }

                var start = model.Fecha.Date;
                var end = start.AddDays(1);

                var listaVariables = new List<CertificadoCaracteristicaDto>();

                if (model.VariablesY != null && model.VariablesY.Count > 0)
                {
                    foreach (var codigo in model.VariablesY)
                    {
                        var dataResult = await getDataQuality.getResultsByVarX(
                            token,
                            _settings.BaseUrl + _settings.QueryResultVarY_X + model.Planta,
                            model.Planta,
                            model.Line,
                            start,
                            end,
                            codigo);

                        var rows = dataResult?.result?.ToList() ?? new List<result_Resultados>();

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
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request failed in GenerarCertificado");
                return BadRequest("External service unavailable.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GenerarCertificado");
                return StatusCode(500, "Internal server error.");
            }
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
