// Controllers/ControlLimitsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using dashboardQ40.Services;
using dashboardQ40.Models;
using Newtonsoft.Json;
using static dashboardQ40.Models.Models;
using System.Globalization;
using System.Data;

namespace dashboardQ40.Controllers
{
    public sealed class ControlLimitsController : Controller
    {
        private readonly WebServiceSettings _settings;
        private readonly AuthService _authService;
        private readonly ILogger<DashboardController> _logger;
        private readonly IConfiguration _configuration;

        public ControlLimitsController(
           IOptions<WebServiceSettings> settings,
           AuthService authService,
           IConfiguration configuration,
           ILogger<DashboardController> logger)
        {
            _settings = settings.Value;
            _authService = authService;
            _configuration = configuration;
            _logger = logger;
        }

        // Helpers cortos para no repetir
        private string Company => _configuration.GetConnectionString("company");
        private string ConnStr => _configuration.GetConnectionString("CaptorConnection");

        private static IEnumerable<Dictionary<string, object?>> ToRows(DataTable dt)
            => dt.AsEnumerable()
                 .Select(r => dt.Columns.Cast<DataColumn>()
                 .ToDictionary(c => c.ColumnName, c => r[c] == DBNull.Value ? null : r[c]));

        [HttpGet]
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
    }
}
