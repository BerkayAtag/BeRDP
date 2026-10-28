using System.Net;
using System.Security.Claims;
using System.Text;
using BeRdp.Backend.Data;
using BeRdp.Backend.Hubs;
using BeRdp.Backend.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
    .Select(origin => origin.Trim())
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? Array.Empty<string>();

var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
var resolvedConnectionString = ResolveSqliteConnectionString(
    defaultConnectionString,
    builder.Configuration,
    builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(resolvedConnectionString));

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IActiveDirectoryConfigurationService, ActiveDirectoryConfigurationService>();
builder.Services.AddScoped<IActiveDirectoryService, ActiveDirectoryService>();
builder.Services.AddScoped<UserAuthorizationService>();
builder.Services.AddScoped<SessionTokenValidator>();
builder.Services.AddSingleton<IRdpAvailabilityService, RdpAvailabilityService>();
builder.Services.AddScoped<BrowserRdpGateway>();
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();
var dataProtectionDirectory = Path.Combine(
    Path.GetDirectoryName(new SqliteConnectionStringBuilder(resolvedConnectionString).DataSource)
        ?? builder.Environment.ContentRootPath,
    "DataProtectionKeys");
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("BeRDP")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionDirectory));
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi();
}
builder.Services.AddSignalR();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(options =>
{
    // Shared NAT/VDI egress addresses can carry many legitimate users, so the
    // default must be high enough for a shift-start login burst while still
    // throttling brute-force attempts. Override with Security:LoginRateLimitPerMinute.
    var loginRateLimit = Math.Clamp(
        builder.Configuration.GetValue("Security:LoginRateLimitPerMinute", 60), 10, 600);
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = loginRateLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Çok fazla giriş denemesi. Bir dakika sonra tekrar deneyin." },
            cancellationToken);
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientOrigins", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy
                .SetIsOriginAllowed(_ => false)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key en az 32 byte olmalıdır.");
}
if (!builder.Environment.IsDevelopment() && signingKey.StartsWith("Replace_This_", StringComparison.Ordinal))
{
    throw new InvalidOperationException("Üretim için Jwt:Key değiştirilmeli. Server Setup bu anahtarı ilk kurulumda üretir.");
}

var authentication = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
if (builder.Configuration.GetValue<bool>("WindowsAuthentication:Enabled"))
{
    if (!OperatingSystem.IsWindows())
    {
        throw new InvalidOperationException("BeRDP Windows SSO requires a Windows backend.");
    }
    authentication.AddNegotiate();
}
authentication.AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var validator = context.HttpContext.RequestServices.GetRequiredService<SessionTokenValidator>();
                if (context.Principal is null ||
                    !await validator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
                {
                    context.Fail("Oturum iptal edilmiş. Yeniden giriş yapın.");
                }
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/assignments"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "session_expired",
                    message = "Oturum geçersiz, iptal edilmiş veya süresi dolmuş. Lütfen yeniden giriş yapın."
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "admin_required",
                    message = "Bu işlem için yönetici yetkisi gerekiyor."
                });
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin").RequireClaim("CanAccessAdminConsole", "true"));
    options.AddPolicy("AdminConsoleAccess", policy =>
        policy.RequireAssertion(context =>
        {
            var canAccess = context.User.FindFirst("CanAccessAdminConsole")?.Value == "true";
            var consoleRole = context.User.FindFirst("ConsoleRole")?.Value;
            return canAccess && consoleRole == "Admin";
        }));
    options.AddPolicy("AdminConsoleReadOnly", policy =>
        policy.RequireAssertion(context =>
        {
            var canAccess = context.User.FindFirst("CanAccessAdminConsole")?.Value == "true";
            return canAccess;
        }));
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BeRDP Backend API",
        Version = "v1",
        Description = "RDP assignment and management backend for BeRDP MVP."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Use: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    if (!context.Request.IsHttps &&
        (context.Connection.RemoteIpAddress is not { } peer || !IPAddress.IsLoopback(peer)))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = "Uzak API erişimi HTTPS gerektirir." });
        return;
    }
    await next();
});
app.UseCors("ClientOrigins");

var allowedIpAddresses = builder.Configuration.GetSection("Security:AllowedIpAddresses").Get<string[]>() ?? Array.Empty<string>();
var allowedIpSet = new HashSet<string>(
    allowedIpAddresses
        .Select(ip => ip.Trim())
        .Where(ip => !string.IsNullOrWhiteSpace(ip))
        .Select(ip =>
        {
            if (!IPAddress.TryParse(ip, out var parsedAddress))
            {
                throw new InvalidOperationException($"Security:AllowedIpAddresses içinde geçersiz IP bulundu: {ip}");
            }

            return parsedAddress.MapToIPv6().ToString();
        }),
    StringComparer.OrdinalIgnoreCase);

if (allowedIpSet.Count > 0)
{
    app.Use(async (context, next) =>
    {
        var remoteIpAddress = context.Connection.RemoteIpAddress;
        var normalizedRemoteIp = remoteIpAddress?.MapToIPv6().ToString();
        var localHealthProbe = remoteIpAddress is not null &&
            IPAddress.IsLoopback(remoteIpAddress) && context.Request.Path == "/health";
        var isAllowed = localHealthProbe || (normalizedRemoteIp is not null && allowedIpSet.Contains(normalizedRemoteIp));
        if (!isAllowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { message = "İstek yapan IP whitelist dışında." });
            return;
        }

        await next();
    });
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapHub<AssignmentsHub>("/hubs/assignments");
app.MapGet("/health", async Task<IResult> (AppDbContext dbContext, CancellationToken cancellationToken) =>
    await dbContext.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ok" })
        : Results.Problem("Veritabanına erişilemiyor.", statusCode: StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous();

app.MapGet("/download/client-setup", (IWebHostEnvironment env) =>
{
    var candidatePaths = new[]
    {
        Path.Combine(env.ContentRootPath, "installers", "BeRDP.Client.Setup.exe"),
        Path.Combine(env.ContentRootPath, "dist", "installers", "BeRDP.Client.Setup.exe"),
        Path.Combine(env.ContentRootPath, "..", "..", "dist", "installers", "BeRDP.Client.Setup.exe"),
        Path.Combine(env.ContentRootPath, "..", "..", "dist", "staging", "installers", "BeRDP.Client.Setup.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "BeRDP", "Server", "installers", "BeRDP.Client.Setup.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BeRDP", "installers", "BeRDP.Client.Setup.exe")
    };

    foreach (var path in candidatePaths)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return Results.File(fullPath, contentType: "application/octet-stream", fileDownloadName: "BeRDP.Client.Setup.exe");
            }
        }
        catch { }
    }

    return Results.NotFound(new { message = "İstemci kurulum dosyası (BeRDP.Client.Setup.exe) sunucuda bulunamadı." });
}).AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(dbContext);
    if (builder.Configuration["reset-admin"] is string resetUsername)
    {
        BootstrapAdministrator.Reset(dbContext, resetUsername, BootstrapAdministrator.ReadPassword());
        Console.WriteLine("Yerel yönetici parolası yenilendi ve eski oturumlar iptal edildi.");
        return;
    }
    if (builder.Configuration["bootstrap-admin"] is string bootstrapUsername)
    {
        BootstrapAdministrator.Create(dbContext, bootstrapUsername, BootstrapAdministrator.ReadPassword());
        Console.WriteLine("İlk yönetici oluşturuldu. Parola hiçbir dosyaya veya loga yazılmadı.");
        return;
    }
}

app.Run();

static string ResolveSqliteConnectionString(
    string connectionString,
    IConfiguration configuration,
    string contentRootPath)
{
    var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(sqliteBuilder.DataSource))
    {
        throw new InvalidOperationException("SQLite connection string must include Data Source.");
    }

    if (Path.IsPathRooted(sqliteBuilder.DataSource))
    {
        var absoluteDirectory = Path.GetDirectoryName(sqliteBuilder.DataSource);
        if (!string.IsNullOrWhiteSpace(absoluteDirectory))
        {
            Directory.CreateDirectory(absoluteDirectory);
        }

        return sqliteBuilder.ToString();
    }

    var configuredDataDirectory = configuration["Storage:DataDirectory"];
    var baseDataDirectory = configuredDataDirectory;
    if (string.IsNullOrWhiteSpace(baseDataDirectory))
    {
        baseDataDirectory = WindowsServiceHelpers.IsWindowsService()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BeRDP")
            : contentRootPath;
    }

    if (!Path.IsPathRooted(baseDataDirectory))
    {
        baseDataDirectory = Path.Combine(contentRootPath, baseDataDirectory);
    }

    var fullBaseDirectory = Path.GetFullPath(baseDataDirectory);
    Directory.CreateDirectory(fullBaseDirectory);

    sqliteBuilder.DataSource = Path.Combine(fullBaseDirectory, sqliteBuilder.DataSource);
    return sqliteBuilder.ToString();
}
