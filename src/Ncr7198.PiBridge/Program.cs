using Microsoft.AspNetCore.Http.Json;
using Ncr7198.PiBridge;

var builder = WebApplication.CreateBuilder(args);
var options = builder.Configuration.GetSection("Bridge").Get<BridgeOptions>() ?? new BridgeOptions();

if (options.MaxOutstandingJobs != 3) throw new InvalidOperationException("Bridge:MaxOutstandingJobs must be 3.");
if (options.PrintIdLifetimeHours != 24) throw new InvalidOperationException("Bridge:PrintIdLifetimeHours must be 24.");
if (options.Transport is not ("Auto" or "Device" or "File"))
    throw new InvalidOperationException("Bridge:Transport must be Auto, Device, or File.");
if (!OperatingSystem.IsWindows() && options.Transport != "File" && !options.DevicePath.StartsWith("/dev/", StringComparison.Ordinal))
    throw new InvalidOperationException("Bridge:DevicePath must be a device below /dev/.");

builder.WebHost.UseUrls(options.ListenUrl);
builder.WebHost.ConfigureKestrel(server => server.Limits.MaxRequestBodySize = 32 * 1024);
builder.Services.Configure<JsonOptions>(json => json.SerializerOptions.PropertyNameCaseInsensitive = true);
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<ReceiptRenderer>();
builder.Services.AddSingleton<IPrinterTransport, PrinterTransport>();
builder.Services.AddSingleton<PrintCoordinator>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PrintCoordinator>());

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", (IPrinterTransport printer) => Results.Ok(new
{
    service = "NCR 7198 Raspberry Pi Bridge",
    transport = printer.Description,
    printerAvailable = printer.IsAvailable()
}));

app.MapPost("/api/preview", (PrintRequest request, ReceiptRenderer renderer) =>
    Execute(() => Results.Ok(renderer.Render(request).Preview)));

app.MapPost("/api/print", async (PrintRequest request, ReceiptRenderer renderer, PrintCoordinator coordinator) =>
{
    try
    {
        var submission = coordinator.Submit(renderer.Render(request));
        var result = await submission.Result;
        if (submission.IsDuplicate) result = result with { Status = "deduplicated" };
        return Results.Ok(result);
    }
    catch (PrintValidationException exception) { return Error(400, exception.Message); }
    catch (PrintQueueFullException exception) { return Error(429, exception.Message); }
    catch (PrintIdConflictException exception) { return Error(409, exception.Message); }
    catch (IOException exception) { return Error(503, $"Printer write failed: {exception.Message}"); }
    catch (UnauthorizedAccessException exception) { return Error(503, $"Printer access failed: {exception.Message}"); }
});

app.MapFallbackToFile("index.html");
app.Run();

static IResult Execute(Func<IResult> action)
{
    try { return action(); }
    catch (PrintValidationException exception) { return Error(400, exception.Message); }
}

static IResult Error(int statusCode, string message) => Results.Json(new { error = message }, statusCode: statusCode);

public partial class Program { }
