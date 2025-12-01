using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using static dashboardQ40.Models.Models;
using System.Globalization;
using ClosedXML.Excel;

namespace dashboardQ40.Controllers
{
    public class CargaACsController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly IConfiguration _configuration;
        private readonly AuthService _authService;
        private readonly IACPayloadService _acPayloadService;

        public CargaACsController(
            IOptions<WebServiceSettings> settings,
            AuthService authService,
            IConfiguration configuration,
            IACPayloadService acPayloadService)
        {
            _settings = settings.Value;
            _authService = authService;
            _configuration = configuration;
            _acPayloadService = acPayloadService;
        }

        public async Task<IActionResult> ListarACs()
        {
            var token = await _authService.ObtenerTokenCaptor(_settings.Company);
            if (token != null)
            {
                HttpContext.Session.SetString("AuthToken", token.access_token);
            }

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

            return View();
        }

        public IActionResult CargarACs()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ObtenerRows([FromBody] RowsRequest req)
        {
            try
            {
                string connStr = _configuration.GetConnectionString("CaptorConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                    return BadRequest("Falta ConnectionStrings:CaptorConnection en appsettings.json");

                DateTime? start = null, end = null;
                if (!string.IsNullOrWhiteSpace(req.startdate))
                    start = DateTime.ParseExact(req.startdate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(req.enddate))
                    end = DateTime.ParseExact(req.enddate, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1);

                object DbNullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? (object)DBNull.Value : v;
                object DbNullIfNull(DateTime? d) => d.HasValue ? d.Value : (object)DBNull.Value;

                var parametros = new Dictionary<string, object>
                {
                    { "@company",            DbNullIfEmpty(req.company) },
                    { "@workplace",          DbNullIfEmpty(req.workplace) },
                    { "@startdate",          DbNullIfNull(start) },
                    { "@enddate",            DbNullIfNull(end) }
                };

                var rows = await _acPayloadService.ObtenerRowsAsync(parametros, connStr);

                return Json(new { rows, count = rows?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return BadRequest($"ObtenerRows exception: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportRows([FromBody] RowsRequest req)
        {
            try
            {
                string connStr = _configuration.GetConnectionString("CaptorConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                    return BadRequest("Falta ConnectionStrings:CaptorConnection.");

                DateTime? start = null, end = null;
                if (!string.IsNullOrWhiteSpace(req.startdate))
                    start = DateTime.ParseExact(req.startdate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(req.enddate))
                    end = DateTime.ParseExact(req.enddate, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(1);

                object DbNullIfEmpty(string? v) => string.IsNullOrWhiteSpace(v) ? (object)DBNull.Value : v;
                object DbNullIfNull(DateTime? d) => d.HasValue ? d.Value : (object)DBNull.Value;

                var parametros = new Dictionary<string, object>
        {
            { "@company",            DbNullIfEmpty(req.company) },
            { "@workplace",          DbNullIfEmpty(req.workplace) },
            { "@startdate",          DbNullIfNull(start) },
            { "@enddate",            DbNullIfNull(end) }
        };

                var rows = await _acPayloadService.ObtenerRowsAsync(parametros, connStr)
                           ?? new List<ACPayloadRow>();

                var colOrder = _configuration
                    .GetSection("ExcelToSqlMappings:ACPayloadRows:ColumnMappings")
                    .GetChildren()
                    .Select(c => c.Key)
                    .ToList();
                // Grupo de columnas que quieres juntas y en este orden:
                var grupoJunto = new[]
                {
                    "controlProcedure",
                    "controlProcedureNote",
                    "worker",
                    "controlOperation",
                    "controlOperationNote",
                    "resultNumber",
                    "resultAttribute",
                    "resultValue",
                    "controlOperationResultValueNote",
                    "controlOperationType"
                };

                // 1) Empezamos con las columnas del grupo (solo las que realmente existan en colOrder)
                var nuevoOrden = new List<string>();
                nuevoOrden.AddRange(grupoJunto.Where(x => colOrder.Contains(x)));

                // 2) Agregamos el resto de columnas, en el orden original, excluyendo las del grupo
                nuevoOrden.AddRange(colOrder.Where(x => !grupoJunto.Contains(x)));

                // 3) Reemplazamos colOrder por este nuevo orden
                colOrder = nuevoOrden;


                using var wb = new XLWorkbook();
                var ws = wb.AddWorksheet("Autocontroles");

                // Encabezados
                for (int i = 0; i < colOrder.Count; i++)
                    ws.Cell(1, i + 1).Value = colOrder[i];

                // Datos
                int r = 2;
                var props = typeof(ACPayloadRow).GetProperties();

                // Normalizamos los nombres de columnas por si vienen con espacios
                var colOrderNorm = colOrder.Select(x => x?.Trim() ?? string.Empty).ToList();

                foreach (var row in rows)
                {
                    for (int c = 0; c < colOrderNorm.Count; c++)
                    {
                        var colName = colOrderNorm[c];

                        // Búsqueda case-insensitive
                        var prop = props.FirstOrDefault(p =>
                            string.Equals(p.Name, colName, StringComparison.OrdinalIgnoreCase));

                        var val = prop?.GetValue(row);
                        ws.Cell(r, c + 1).SetValue(val?.ToString() ?? "");
                    }
                    r++;
                }



                // --------------------------------------------------------------------
                //  🔵 1) Columnas que el usuario DEBE EDITAR
                //     (ajusta la lista si quieres incluir/excluir alguna)
                // --------------------------------------------------------------------
                var columnasEditables = new[]
                {
            "controlProcedureNote",
            "resultAttribute",
            "resultNumber",
            "worker",
            "controlOperationResultValueNote",
            "resultValue"
        };

                foreach (var colName in columnasEditables)
                {
                    int idx = colOrder.FindIndex(x => x == colName);
                    if (idx < 0) continue;   // esa columna no existe en este layout

                    int excelCol = idx + 1;

                    // Fondo para toda la columna (ej. amarillo claro)
                    var col = ws.Column(excelCol);
                    col.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightYellow;

                    // Encabezado un poco más fuerte
                    ws.Cell(1, excelCol).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
                    ws.Cell(1, excelCol).Style.Font.Bold = true;

                    // 🔓 Importante: desbloquear esta columna para que se pueda editar
                    col.Style.Protection.Locked = false;
                }

                // --------------------------------------------------------------------
                //  🔒 2) Proteger la hoja para que SOLO las columnas desbloqueadas
                //       puedan modificarse.
                //       (si quieres, puedes poner contraseña: ws.Protect("ACs2025"))
                // --------------------------------------------------------------------
                ws.Columns().AdjustToContents();

                // Proteger la hoja, pero:
                // - Permitir seleccionar celdas desbloqueadas (para editarlas)
                // - Permitir cambiar ancho de columnas
                var protection = ws.Protect(); // si quieres, puedes poner password: ws.Protect("ACs2025");
                protection.AllowedElements =
                    XLSheetProtectionElements.SelectUnlockedCells |
                    XLSheetProtectionElements.SelectLockedCells |   // opcional, si quieres que también puedan seleccionarlas
                    XLSheetProtectionElements.FormatColumns;


                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                var bytes = ms.ToArray();

                var fileName = $"Autocontroles_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest($"ExportRows exception: {ex.Message}");
            }
        }

        [HttpGet("ObtenerWorkplaces")]
        public async Task<IActionResult> ObtenerWorkplaces(string company)
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
                _settings.BaseUrl + _settings.QueryWorkplaces + company, // tu query
                company,                  // si lo pides, o quítalo
                _settings.trazalog                             // filtro de company
            );

            var list = resp?.result?.Select(w => new {
                workplace = w.workplace,           // id
                workplaceName = w.workplaceName    // nombre
            }) ?? Enumerable.Empty<object>();

            return Json(list);
        }

        [HttpPost]
        public async Task<IActionResult> ImportarExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No se recibió archivo");

            // 1) Leer Excel
            List<AutocontrolExcelRow> filas;
            using (var stream = file.OpenReadStream())
            {
                filas = AutocontrolExcelReader.LeerDesdeExcel(stream);
            }

            // 2) Construir payloads: 1 JSON por IdControlProcedureResult
            var payloads = AutocontrolPayloadBuilder.BuildOnePerControlProcedureResult(filas);

            // 3) Config de SOAP
            var captorCfg = _configuration.GetSection("CaptorAutocontrol");
            string soapUrl = captorCfg.GetValue<string>("PerformActionsUrl");
            string soapNs = captorCfg.GetValue<string>("SoapNamespace");
            string soapAct = captorCfg.GetValue<string>("SoapActionCompleteControlProcedure");
            string user = captorCfg.GetValue<string>("User");
            string company = _settings.Company;

            int enviadosOk = 0;
            int enviadosError = 0;
            var errores = new List<string>();

            // Mapa: CPR → (status, mensaje)
            var statusPorCpr = new Dictionary<string, (string Status, string Mensaje)>();

            // 4) Enviar un payload por CPR
            foreach (var payload in payloads)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(payload);

                    var respXml = await CaptorPerformActionsService.CompleteControlProcedureAsync(
                        soapUrl,
                        soapNs,
                        soapAct,
                        company,
                        user,
                        json
                    );

                    enviadosOk++;
                    statusPorCpr[payload.IdControlProcedureResult] = ("OK", "");
                }
                catch (Exception ex)
                {
                    enviadosError++;
                    var msg = ex.Message;
                    errores.Add($"[SOAP] CPR {payload.IdControlProcedureResult}: {msg}");
                    statusPorCpr[payload.IdControlProcedureResult] = ("ERROR", msg);
                }
            }

            // 5) Armar detalle por FILA de Excel (las columnas amarillas + estado)
            var filasDetalle = filas.Select(f =>
            {
                statusPorCpr.TryGetValue(f.IdControlProcedureResult, out var info);

                if (string.IsNullOrEmpty(info.Status))
                    info = ("NO_ENVIADO", "");

                return new
                {
                    batch = f.Batch,
                    controlOperation = f.ControlOperation,
                    controlOperationNote = f.ControlOperationNote,
                    controlProcedure = f.ControlProcedure,
                    controlProcedureNote = f.ControlProcedureNote,
                    controlProcedureVersion = f.ControlProcedureVersion,
                    launchingDate = f.LaunchingDate,
                    manufacturingOrder = f.ManufacturingOrder,
                    resultAttribute = f.ResultAttribute,
                    resultValue = f.ResultValue,
                    worker = f.Worker,
                    workplace = f.Workplace,
                    status = info.Status,
                    message = info.Mensaje
                };
            }).ToList();

            return Json(new
            {
                totalFilas = filas.Count,
                totalPayloads = payloads.Count,
                enviadosOk,
                enviadosError,
                filasDetalle   // 👈 ESTA ES LA CLAVE
            });
        }




    }
}
