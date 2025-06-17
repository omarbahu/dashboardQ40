using dashboardQ40.DAL;
using dashboardQ40.Functions;
using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using static dashboardQ40.Models.Models;

var builder = WebApplication.CreateBuilder(args);

// 🎯 Configuración de Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 📦 Servicios principales
builder.Services.AddControllersWithViews();

// 🌐 Soporte para recursos de idioma
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "es-ES", "en-US" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();

    options.DefaultRequestCulture = new RequestCulture("es-ES");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
});

// 🌐 Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 🌐 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 🌐 Config externa
builder.Services.Configure<WebServiceSettings>(builder.Configuration.GetSection("WebServiceSettings"));
builder.Services.Configure<VariablesYConfig>(builder.Configuration.GetSection("VariablesY"));

// 🌐 HttpClient para AuthService
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddTransient<AuthService>();

// 🌐 DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DashboardConnection")));

// 🌐 JSON config
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
    options.JsonSerializerOptions.WriteIndented = true;
});

// 🛠️ Build
var app = builder.Build();

// 🔐 Middleware de error y seguridad
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 🌍 Establecer cultura desde sesión


// 🔒 HTTPS, Archivos, Session, Localización
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseMiddleware<SetCultureMiddleware>();

// ✅ Configuración de localización
var localizationOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>()?.Value;
app.UseRequestLocalization(localizationOptions);


app.UseAuthorization();

// 🔄 Ruta principal
app.MapControllerRoute(
    name: "default",
    pattern: app.Environment.IsDevelopment()
        ? "{controller=Home}/{action=Index}/{id?}"
        : "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers();
app.Run();
