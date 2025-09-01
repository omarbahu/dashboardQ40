using dashboardQ40.DAL;
using dashboardQ40.Middlewares;
using dashboardQ40.Models;
using dashboardQ40.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
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
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
});

// ✅ REGISTRA el localizador base para @inject IStringLocalizer
builder.Services.AddSingleton<IStringLocalizer>(sp =>
{
    var factory = sp.GetRequiredService<IStringLocalizerFactory>();
    return factory.Create("Labels", typeof(Program).Assembly.GetName().Name);
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

// 🌐 HttpClient y AuthService
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddTransient<AuthService>();

// 🌐 Base de datos
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DashboardConnection")));

// 🌐 JSON
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
    options.JsonSerializerOptions.WriteIndented = true;
});

// 🛠️ Compilar la app
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

// 🔒 HTTPS, archivos estáticos, routing
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 🧠 Sesión
app.UseSession();

// 🌍 Localización: PRIMERO RequestLocalization
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

// 🌍 Cultura personalizada desde sesión
app.UseMiddleware<SetCultureMiddleware>();

// 🔐 Autorización
app.UseAuthorization();

// 📌 Rutas
app.MapControllerRoute(
    name: "default",
    pattern: app.Environment.IsDevelopment()
        ? "{controller=Home}/{action=Index}/{id?}"
        : "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers();

// 🚀 Ejecutar
app.Run();
