using System.Diagnostics;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

StreamWriter? log = null;
try
{
    if (args is ["--self-test"])
    {
        RunConfigurationTests();
        return 0;
    }
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (args.Length % 2 != 0) throw new ArgumentException("Arguments require --name value pairs.");
    for (var i = 0; i < args.Length; i += 2)
    {
        if (args[i] is not ("--root" or "--mode" or "--sso" or "--log"))
            throw new ArgumentException("Unknown argument: " + args[i]);
        options.Add(args[i], args[i + 1]);
    }
    var root = Path.GetFullPath(options["--root"]);
    var mode = options["--mode"];
    if (mode is not ("prepare" or "install" or "uninstall")) throw new ArgumentException("Invalid service setup mode.");
    var sso = options.GetValueOrDefault("--sso");
    if (sso is not (null or "0" or "1")) throw new ArgumentException("SSO must be 0 or 1.");
    var logPath = Path.GetFullPath(options["--log"]);
    Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
    log = new StreamWriter(logPath, append: true) { AutoFlush = true };
    Write($"Service setup: {mode}, root: {root}");
    var definitions = new[]
    {
        (Name: "BeRdpBackend", Folder: "Backend", Exe: "BeRdp.Backend.exe", Port: 5001, Display: "BeRDP Backend API"),
        (Name: "BeRdpAdminConsole", Folder: "AdminConsole", Exe: "BeRdp.AdminConsole.exe", Port: 5173, Display: "BeRDP Admin Console")
    };
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    if (mode == "install")
    {
        foreach (var definition in definitions)
            if (!File.Exists(Path.Combine(root, definition.Folder, definition.Exe)))
                throw new FileNotFoundException("Missing service executable: " + definition.Exe);
    }
    foreach (var definition in definitions)
    {
        using var service = FindService(definition.Name);
        if (service is null) continue;
        var registeredPath = Microsoft.Win32.Registry.GetValue(
            $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\{definition.Name}", "ImagePath", null) as string;
        var expectedPath = Path.Combine(root, definition.Folder, definition.Exe);
        if (registeredPath is null ||
            !registeredPath.StartsWith($"\"{expectedPath}\"", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Service {definition.Name} belongs to a different installation. No stop/delete performed.");
    }
    foreach (var definition in definitions)
    {
        using var service = FindService(definition.Name);
        if (service is null) { Write("Service not installed: " + definition.Name); continue; }
        service.Refresh();
        if (service.Status != ServiceControllerStatus.Stopped)
        {
            if (service.Status != ServiceControllerStatus.StopPending) service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(45));
        }
        if (mode == "uninstall") await Sc("delete", definition.Name);
    }
    if (mode == "install")
    {
        SaveConfiguration(root, sso);
        foreach (var definition in definitions)
        {
            var folder = Path.Combine(root, definition.Folder);
            var binary = $"\"{Path.Combine(folder, definition.Exe)}\" --contentRoot \"{folder}\" --urls http://127.0.0.1:{definition.Port}";
            using var existing = FindService(definition.Name);
            if (existing is null)
                await Sc("create", definition.Name, "binPath=", binary, "start=", "auto", "DisplayName=", definition.Display);
            else
                // Omitting obj/password preserves an existing custom service account.
                await Sc("config", definition.Name, "binPath=", binary, "start=", "auto");
            await Sc("failure", definition.Name, "reset=", "86400", "actions=", "restart/5000/restart/5000/restart/5000");
            await Sc("failureflag", definition.Name, "1");
            using var service = new ServiceController(definition.Name);
            service.Start();
            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(45));
            var healthy = false;
            for (var attempt = 0; attempt < 15; attempt++)
            {
                try
                {
                    using var response = await client.GetAsync($"http://localhost:{definition.Port}/health");
                    if (response.IsSuccessStatusCode) { healthy = true; break; }
                    Write($"Health not ready: {definition.Name}, HTTP {(int)response.StatusCode}");
                }
                catch (HttpRequestException ex) { Write($"Health pending: {definition.Name}: {ex.Message}"); }
                catch (TaskCanceledException ex) { Write($"Health timeout: {definition.Name}: {ex.Message}"); }
                await Task.Delay(1000);
            }
            if (!healthy) throw new InvalidOperationException("Service health check failed: " + definition.Name);
        }
    }
    Write("SUCCESS: service setup completed.");
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
    ArgumentException or KeyNotFoundException or JsonException or System.ComponentModel.Win32Exception or
    System.ServiceProcess.TimeoutException)
{
    Console.Error.WriteLine(ex);
    log?.WriteLine($"FAILED: {ex}");
    return 1;
}
finally { log?.Dispose(); }

void Write(string message)
{
    Console.WriteLine(message);
    log?.WriteLine($"{DateTimeOffset.Now:O} {message}");
}

ServiceController? FindService(string name)
{
    ServiceController? match = null;
    foreach (var service in ServiceController.GetServices())
    {
        if (service.ServiceName.Equals(name, StringComparison.OrdinalIgnoreCase)) match = service;
        else service.Dispose();
    }
    return match;
}

async Task Sc(params string[] arguments)
{
    var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "sc.exe"))
    {
        UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
    };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start Windows service control.");
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    Write(await output);
    var errors = await error;
    if (process.ExitCode != 0) throw new InvalidOperationException($"sc {arguments[0]} failed ({process.ExitCode}): {errors}");
}

void SaveConfiguration(string root, string? sso, bool testMode = false)
{
    var path = Path.Combine(root, "Backend", "appsettings.json");
    var settings = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
        ?? throw new InvalidOperationException("Backend configuration is empty.");
    var serviceIdentity = PrivateStorage.BackendIdentity();
    if (!testMode) PrivateStorage.Protect(Path.Combine(root, "Backend"), serviceIdentity);
    var jwt = settings["Jwt"]?.AsObject() ?? throw new InvalidOperationException("Jwt configuration is missing.");
    var key = jwt["Key"]?.GetValue<string>();
    if (string.IsNullOrWhiteSpace(key) || key.StartsWith("Replace_This_", StringComparison.Ordinal))
        jwt["Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    else if (Encoding.UTF8.GetByteCount(key) < 32)
        throw new InvalidOperationException("Existing signing key is shorter than 32 bytes.");
    var storage = settings["Storage"]?.AsObject() ?? new JsonObject();
    if (settings["Storage"] is null) settings["Storage"] = storage;
    if (string.IsNullOrWhiteSpace(storage["DataDirectory"]?.GetValue<string>()))
        storage["DataDirectory"] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BeRDP");
    var dataDirectory = storage["DataDirectory"]!.GetValue<string>();
    if (!Path.IsPathRooted(dataDirectory)) dataDirectory = Path.Combine(root, "Backend", dataDirectory);
    var connection = new System.Data.Common.DbConnectionStringBuilder
    {
        ConnectionString = settings["ConnectionStrings"]?["DefaultConnection"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Database connection string is missing.")
    };
    var databasePath = new[] { "Data Source", "DataSource", "Filename" }
        .Select(key => connection.TryGetValue(key, out var value) ? value?.ToString() : null)
        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
        ?? throw new InvalidOperationException("Database path is missing.");
    var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(dataDirectory, databasePath)))!;
    if (!testMode) PrivateStorage.Protect(dataDirectory, serviceIdentity);
    if (!testMode && !string.Equals(databaseDirectory, Path.GetFullPath(dataDirectory), StringComparison.OrdinalIgnoreCase))
        PrivateStorage.Protect(databaseDirectory, serviceIdentity);
    if (sso is not null)
    {
        var windows = settings["WindowsAuthentication"]?.AsObject() ?? new JsonObject();
        if (settings["WindowsAuthentication"] is null) settings["WindowsAuthentication"] = windows;
        windows["Enabled"] = sso == "1";
    }
    var cors = settings["Cors"]?.AsObject() ?? new JsonObject();
    if (settings["Cors"] is null) settings["Cors"] = cors;
    var origins = cors["AllowedOrigins"]?.AsArray().Select(x =>
        x?.GetValue<string>() ?? throw new InvalidOperationException("CORS origin cannot be null.")).ToHashSet(StringComparer.OrdinalIgnoreCase)
        ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    origins.Add("http://localhost:5173");
    origins.Add($"http://{Environment.MachineName}:5173");
    try
    {
        var host = System.Net.Dns.GetHostEntry(Environment.MachineName);
        origins.Add($"http://{host.HostName}:5173");
        foreach (var ip in host.AddressList) origins.Add(new UriBuilder("http", ip.ToString(), 5173).Uri.GetLeftPart(UriPartial.Authority));
    }
    catch (System.Net.Sockets.SocketException ex) { Write("WARNING: DNS discovery failed; configure CORS aliases manually: " + ex.Message); }
    cors["AllowedOrigins"] = new JsonArray(origins.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
    var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    var backup = path + ".setup-" + Guid.NewGuid().ToString("N") + ".bak";
    try
    {
        File.WriteAllText(temporary, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Replace(temporary, path, backup);
        Write("Previous configuration preserved at: " + backup);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
}

void RunConfigurationTests()
{
    var root = Path.Combine(Path.GetTempPath(), "BeRdp-ServiceSetupTests-" + Guid.NewGuid().ToString("N"));
    var backend = Path.Combine(root, "Backend");
    Directory.CreateDirectory(backend);
    try
    {
        var path = Path.Combine(backend, "appsettings.json");
        const string original = """{"ConnectionStrings":{"DefaultConnection":"Data Source=berdp.db"},"Jwt":{"Key":"existing-key-more-than-thirty-two-bytes"},"Custom":{"Value":"preserve"},"BrowserRdp":{"JsonSecretKey":"test-only-preserved-key"}}""";
        File.WriteAllText(path, original);
        SaveConfiguration(root, "1", true);
        var saved = JsonNode.Parse(File.ReadAllText(path))!;
        if (saved["Custom"]!["Value"]!.GetValue<string>() != "preserve" ||
            saved["BrowserRdp"]!["JsonSecretKey"]!.GetValue<string>() != "test-only-preserved-key" ||
            !saved["WindowsAuthentication"]!["Enabled"]!.GetValue<bool>())
            throw new InvalidOperationException("Configuration preservation/SSO test failed.");
        var backup = Directory.GetFiles(backend, "*.bak").Single();
        if (File.ReadAllText(backup) != original) throw new InvalidOperationException("Exact backup test failed.");
        SaveConfiguration(root, null, true);
        if (!JsonNode.Parse(File.ReadAllText(path))!["WindowsAuthentication"]!["Enabled"]!.GetValue<bool>())
            throw new InvalidOperationException("Upgrade SSO preservation test failed.");
        SaveConfiguration(root, "0", true);
        if (JsonNode.Parse(File.ReadAllText(path))!["WindowsAuthentication"]!["Enabled"]!.GetValue<bool>() ||
            Directory.GetFiles(backend, "*.tmp").Length != 0 || Directory.GetFiles(backend, "*.bak").Length != 3)
            throw new InvalidOperationException("SSO disable or temporary file cleanup test failed.");
        var previous = File.ReadAllBytes(path);
        using (var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var rejected = false;
            try { SaveConfiguration(root, "1", true); }
            catch (IOException) { rejected = true; }
            catch (UnauthorizedAccessException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Locked-file write was not rejected.");
        }
        if (!File.ReadAllBytes(path).SequenceEqual(previous) || Directory.GetFiles(backend, "*.tmp").Length != 0)
            throw new InvalidOperationException("Locked-file failure modified settings or leaked temporary files.");
        Write("SUCCESS: native configuration replacement, backup, preservation, SSO and locked-file checks passed.");
    }
    finally { Directory.Delete(root, recursive: true); }
}
