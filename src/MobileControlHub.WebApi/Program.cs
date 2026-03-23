using MobileControlHub.Domain.Interfaces;
using MobileControlHub.Infrastructure.Data;
using MobileControlHub.Infrastructure.Helpers;
using MobileControlHub.Infrastructure.Services;
using MobileControlHub.WebApi.Hubs;

var builder = WebApplication.CreateBuilder(args);

// CORS - allow the React dev server and any origin for local network access
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// Register infrastructure services
builder.Services.AddSingleton<DatabaseManager>();
// ProcessRunner is a static utility class - no DI registration needed
builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<ILogService, LogService>();
builder.Services.AddSingleton<IAdbService, AdbService>();
builder.Services.AddSingleton<IScrcpyService, ScrcpyService>();
builder.Services.AddSingleton<IRustDeskService, RustDeskService>();
builder.Services.AddSingleton<IDeviceMonitorService, DeviceMonitorService>();
builder.Services.AddSingleton<ITwilioService, TwilioService>();
builder.Services.AddSingleton<IGenymotionService, GenymotionService>();
builder.Services.AddSingleton<ICloudPlatformService, CloudPlatformService>();
builder.Services.AddSingleton<ICuttlefishService, CuttlefishService>();

// Serve static files (React build output)
builder.Services.AddDirectoryBrowser();

var app = builder.Build();

// Resolve working directory to the solution/repo root so that relative tool paths
// like "tools\adb.exe" resolve correctly (instead of relative to the WebApi project dir).
var contentRoot = app.Environment.ContentRootPath;
var candidateDir = new DirectoryInfo(contentRoot);
while (candidateDir != null)
{
    var slnPath = Path.Combine(candidateDir.FullName, "MobileControlHub.sln");
    var toolsPath = Path.Combine(candidateDir.FullName, "tools");
    if (File.Exists(slnPath) || Directory.Exists(toolsPath))
    {
        Environment.CurrentDirectory = candidateDir.FullName;
        break;
    }
    candidateDir = candidateDir.Parent;
}

// Initialize the database
var dbManager = app.Services.GetRequiredService<DatabaseManager>();
await dbManager.InitializeAsync();

// Start the device monitor
var monitor = app.Services.GetRequiredService<IDeviceMonitorService>();
_ = monitor.StartAsync();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<DeviceHub>("/hubs/devices").RequireCors("SignalR");

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
