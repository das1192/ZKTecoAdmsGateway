using ZKTecoGateway;
using ZKTecoGateway.Config;
using ZKTecoGateway.Middleware;
using ZKTecoGateway.Services;

var builder = WebApplication.CreateBuilder(args);

var gatewayConfig = builder.Configuration
    .GetSection("GatewayConfig")
    .Get<GatewayConfig>() ?? new GatewayConfig();

builder.Services.AddSingleton(gatewayConfig);
builder.Services.AddSingleton<DeviceRegistryService>();
builder.Services.AddSingleton<ForwardingService>();
builder.Services.AddSingleton<DeviceStateService>();
builder.Services.AddSingleton<AttendanceArchiveService>();
builder.Services.AddHostedService<ScheduledPullService>();
builder.Services.Configure<ScheduledPullConfig>(
builder.Configuration.GetSection("ScheduledPull"));
builder.Services.AddHttpClient("ForwardClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("User-Agent", "ZKTecoGateway/1.0");
});

builder.Services.AddControllers();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();
app.UseStaticFiles();
// Raw request logger
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("▶ {Method} {Path}{Query}",
        context.Request.Method, context.Request.Path, context.Request.QueryString);
    await next();
});

app.UseMiddleware<AdminKeyMiddleware>();
app.MapControllers();

app.MapGet("/", (HttpRequest request) =>
{
    var key = request.Query["key"].ToString();
    var adminKey = builder.Configuration["GatewaySettings:AdminKey"] ?? "";

    // No admin key configured
    if (string.IsNullOrEmpty(adminKey))
        return Results.Redirect("/dashboard");

    // Valid key
    if (key == adminKey)
        return Results.Redirect("/dashboard");

    // Show login page
    return Results.Redirect("/login.html");
});

// Serve dashboard
app.MapGet("/dashboard", (IWebHostEnvironment env) =>
{
    return Results.File(
        Path.Combine(env.WebRootPath, "dashboard.html"),
        "text/html");
});


var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("=== ZKTeco ADMS Gateway ===");

foreach (var c in gatewayConfig.Clients)
{
    logger.LogInformation(
        "[{Id}] {Name} → {Urls} ({N} devices)",
        c.ClientId,
        c.ClientName,
        string.Join(" | ", c.ForwardUrls),
        c.DeviceSerialNumbers.Count);
}

app.Run();
