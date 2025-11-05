using dashboardQ40.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace dashboardQ40.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult NoAutorizado()
        {
            return View();
        }
        
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult BpmDashboard()
    => View("~/Views/Home/BpmDashboard.cshtml");

        public IActionResult BPM()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
