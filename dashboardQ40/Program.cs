using dashboardQ40.DAL;
using dashboardQ40.Middlewares;
using dashboardQ40.Models;
using dashboardQ40.Repositories;
using dashboardQ40.Services;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Serilog;
using System.Globalization;
using static dashboardQ40.Models.ControlLimitsModel;
using static dashboardQ40.Models.Models;

var builder = WebApplication.CreateBuilder(args);

// 🎯 Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 📦 MVC
builder.Services.AddControllersWithViews();

// 🌐 Recursos de idioma
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// 🌐 Localización: default en-US, sin Accept-Language
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "es-ES", "en-US" }
        .Select(c => new CultureInfo(c)).ToList();

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;

    // 👇 Solo Cookie (y opcional QueryString). Sin Accept-Language.
    options.RequestCultureProviders = new IRequestCultureProvider[]
    {
        new CookieRequestCultureProvider(),
        // new QueryStringRequestCultureProvider(),  // <-- opcional
    };
});

// ✅ Localizador base para @inject IStringLocalizer Localizer
builder.Services.AddSingleton<IStringLocalizer>(sp =>
{
    var factory = sp.GetRequiredService<IStringLocalizerFactory>();
    return factory.Create("Labels", typeof(Program).Assembly.GetName().Name);
});

// 🧠 Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 🔧 Swagger (solo Dev)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IACPayloadService, ACPayloadService>();

// ⚙️ Config externa
builder.Services.Configure<WebServiceSettings>(builder.Configuration.GetSection("WebServiceSettings"));
builder.Services.Configure<VariablesYConfig>(builder.Configuration.GetSection("VariablesY"));

// 🌍 HttpClient y AuthService
builder.Services.AddHttpClient<AuthService>();
builder.Services.AddTransient<AuthService>();

builder.Services.Configure<ControlLimitsDefaults>(builder.Configuration.GetSection("ControlLimits:Defaults"));
builder.Services.Configure<ControlLimitsWsOptions>(builder.Configuration.GetSection("ControlLimits:WebService"));

builder.Services.AddScoped<ControlLimitsService>();
builder.Services.AddScoped<IAutocontrolRepository, WSControlLimitsRepository>();


builder.Services.AddScoped<ControlProcedureVersioningService>();
builder.Services.AddScoped<WSCaptorControlProcedureRepository>();


// 💾 DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DashboardConnection")));

// 🔤 JSON
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve;
    options.JsonSerializerOptions.WriteIndented = true;
});

var app = builder.Build();

// === HTTPS flag
var enforceHttps = builder.Configuration.GetValue<bool>("Https:Enforce", false);
Log.Information("STARTUP marker | EnforceHttps={EnforceHttps} | {Now}", enforceHttps, DateTimeOffset.Now);

// 🛡️ Errores y HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (enforceHttps) app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (enforceHttps) app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

// 🧠 Sesión
app.UseSession();

// 🌍 RequestLocalization options + log de providers
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
Log.Information("CULTURE START | Default={Default} | Providers={Providers}",
    localizationOptions.DefaultRequestCulture.UICulture.Name,
    string.Join(", ", localizationOptions.RequestCultureProviders.Select(p => p.GetType().Name)));

// 🔎 DIAG: antes de RL (qué trae el request)
app.Use(async (ctx, next) =>
{
    var cookie = ctx.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
    var sess = ctx.Session.GetString("culture");
    var acc = ctx.Request.Headers["Accept-Language"].ToString();

    Log.Information("CULTURE BEFORE | Cookie={Cookie} | Session={Session} | Accept-Language={Accept}",
        cookie, sess, acc);

    await next();
});

// 🌍 Tu middleware de cultura debe ESCRIBIR la cookie si hay Session["culture"]
// (no cambies CultureInfo aquí; solo sincroniza la cookie)
app.UseMiddleware<SetCultureMiddleware>();

// 🌍 RequestLocalization: fija la cultura final del request leyendo la cookie
app.UseRequestLocalization(localizationOptions);

// 🔎 DIAG: después de RL (qué quedó seleccionado y quién lo decidió)
app.Use(async (ctx, next) =>
{
    var feature = ctx.Features.Get<IRequestCultureFeature>();
    var providerName = feature?.Provider?.GetType().Name ?? "(none)";

    Log.Information("CULTURE AFTER RL | Current={Cur} | UI={UI} | Provider={Provider}",
        CultureInfo.CurrentCulture.Name,
        CultureInfo.CurrentUICulture.Name,
        providerName);

    await next();
});

// 🔎 DIAG: final por si algo cambia después
app.Use(async (ctx, next) =>
{
    Log.Information("CULTURE FINAL | Current={Cur} | UI={UI}",
        CultureInfo.CurrentCulture.Name,
        CultureInfo.CurrentUICulture.Name);
    await next();
});

app.UseAuthorization();

// 📌 Rutas
app.MapControllerRoute(
    name: "default",
    pattern: app.Environment.IsDevelopment()
        ? "{controller=Home}/{action=Index}/{id?}"
        : "{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
