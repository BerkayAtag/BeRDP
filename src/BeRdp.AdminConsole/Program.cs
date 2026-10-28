using BeRdp.AdminConsole;
using System.Text.Json;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

builder.Services.AddOptions<AdminConsoleOptions>()
    .Bind(builder.Configuration.GetSection("AdminConsole"))
    .Validate(options => Uri.TryCreate(options.BackendBaseUrl, UriKind.Absolute, out var uri) &&
                         (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
        "AdminConsole:BackendBaseUrl geçerli bir HTTP/HTTPS adresi olmalıdır.")
    .ValidateOnStart();

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' https: http://localhost:* http://127.0.0.1:*; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/config.js", (HttpContext context, IOptions<AdminConsoleOptions> options) =>
{
    var backendUri = new Uri(options.Value.BackendBaseUrl);
    var browserHost = context.Request.Host.Host;
    var backendUrl = backendUri.IsLoopback && !IsLoopbackHost(browserHost)
        ? new UriBuilder(backendUri) { Host = browserHost }.Uri.AbsoluteUri.TrimEnd('/')
        : backendUri.AbsoluteUri.TrimEnd('/');
    context.Response.Headers.CacheControl = "no-store";
    var script = $"window.__APP_CONFIG__ = {{ backendBaseUrl: {JsonSerializer.Serialize(backendUrl)} }};";
    return Results.Text(script, "application/javascript");
});
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapFallbackToFile("index.html");

app.Run();

static bool IsLoopbackHost(string host) =>
    string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
    (System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address));
