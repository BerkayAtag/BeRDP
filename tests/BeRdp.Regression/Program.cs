using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using BeRdp.Backend.Data;
using BeRdp.Backend.Services;
using LoginRequest = BeRdp.Backend.Dtos.Auth.LoginRequest;
using BackendLoginResponse = BeRdp.Backend.Dtos.Auth.LoginResponse;
using BeRdp.Client.Models;
using BeRdp.Client.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BeRdp.Backend.Controllers;
using BeRdp.Backend.Dtos.Requests;
using BeRdp.Backend.Dtos.Responses;
using BeRdp.Backend.Hubs;
using BeRdp.Backend.Models;

var passed = 0;
void Check(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException(message); }
    passed++;
    Console.WriteLine($"PASS {message}");
}
void ExpectInvalid(Action action, string message)
{
    try { action(); }
    catch (InvalidOperationException) { Check(true, message); return; }
    throw new InvalidOperationException("Expected rejection: " + message);
}

Check(ActiveDirectoryLdapPath.Validate("corp.example", "OU=Staff, OU=Corp, DC=corp, DC=example") is null,
    "CORP OU accepts spaces after separators");
Check(ActiveDirectoryLdapPath.Validate("corp.example", @"OU=Team\, A,OU=Corp,DC=corp,DC=example") is null,
    "Escaped commas in OU names are accepted");
Check(ActiveDirectoryLdapPath.Validate("corp.example", "OU=People,DC=wrong,DC=local") is not null,
    "OU outside configured domain is rejected");
Check(ActiveDirectoryLdapPath.Validate("corp.example", "LDAP://") is not null,
    "Empty LDAP URL is rejected");
var ldapPath = ActiveDirectoryLdapPath.Resolve("corp.example", "LDAP://dc01.corp.example/OU=Staff,DC=corp,DC=example");
Check(ldapPath.Server == "dc01.corp.example" && ldapPath.DistinguishedName.StartsWith("OU=Staff"),
    "LDAP URL resolves server and DN consistently");

using var database = new SqliteConnection("Data Source=:memory:");
database.Open();
var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(database).Options;
using var context = new AppDbContext(dbOptions);
DbInitializer.Initialize(context);
Check(!context.Users.Any() && !context.Machines.Any(), "Production startup has no accounts or demo machines");
BootstrapAdministrator.Create(context, "initial-admin", "Unique-regression-password!");
Check(context.Users.Single().IsAdmin && context.Users.Single().CanAccessAdminConsole, "Explicit bootstrap creates an authorized administrator");
ExpectInvalid(() => BootstrapAdministrator.Create(context, "another-admin", "Unique-regression-password!"), "Bootstrap cannot run against existing users");
Environment.SetEnvironmentVariable(BootstrapAdministrator.SetupPasswordVariable, "Setup-wizard-password-123");
Check(BootstrapAdministrator.ReadPassword() == "Setup-wizard-password-123" &&
      Environment.GetEnvironmentVariable(BootstrapAdministrator.SetupPasswordVariable) is null,
    "Setup wizard password is read once and cleared from the process environment");
var groupAdmin = new User { Username = "group-admin", DisplayName = "Group Admin", IsActiveDirectoryUser = true };
Check(UserAuthorizationService.EffectiveConsoleAccess(groupAdmin, effectiveIsAdmin: true) == (true, ConsoleRole.Admin),
    "AD group admin grant opens the console with full rights");
Check(UserAuthorizationService.EffectiveConsoleAccess(groupAdmin, effectiveIsAdmin: false) == (false, ConsoleRole.Standard),
    "Users without a grant keep their explicit console settings");
var quarantinedAdmin = new User { Username = "old-admin", DisplayName = "Old", IsAdmin = true, CanAccessAdminConsole = false };
Check(!UserAuthorizationService.EffectiveConsoleAccess(quarantinedAdmin, effectiveIsAdmin: true).CanAccess,
    "Explicitly revoked local console access is not re-enabled by the admin flag");
context.Users.RemoveRange(context.Users);
context.SaveChanges();
DemoFixture.Initialize(context);
Check(context.Users.Count() == 12 && context.Machines.Count() == 10 && context.UserMachines.Any(),
    "Initial seed creates admin, console-user, 10 test users and 10 machines with entitlements");
DemoFixture.Initialize(context);
Check(context.Users.Count() == 12 && context.UserMachines.Any(), "Restart does not create extra users or assignments");
Check(BCrypt.Net.BCrypt.Verify("Berdp@Admin2026!Secure#Password",
        context.Users.Single(u => u.Username == "admin").PasswordHash),
    "Seeded admin password matches the documented credential");
var consoleUser = context.Users.Single(u => u.Username == "console-user");
Check(consoleUser.CanAccessAdminConsole && !consoleUser.IsAdmin && consoleUser.ConsoleRole == ConsoleRole.Standard,
    "Seeded console-user has read-only console access");
Check(context.Users.Where(u => u.Username.StartsWith("user")).All(u => !u.CanAccessAdminConsole && !u.IsAdmin),
    "Seeded entitlement users cannot access the admin console");
Check(context.Machines.Count() == 10 && context.UserMachines.Any() && context.MachineAdGroupEntitlements.Any(),
    "Seeded machines have user and AD-group entitlements");
var adminUser = context.Users.Single(u => u.Username == "admin");
adminUser.DisplayName = "Preserved Admin";
context.SaveChanges();
DemoFixture.Initialize(context);
Check(context.Users.Single(u => u.Username == "admin").DisplayName == "Preserved Admin", "Existing bootstrap account settings are preserved");
Check(context.Users.All(user => user.SecurityStamp.Length == 32), "Users have session security stamps");

// Simulate a pre-existing installation that only contains the legacy admin row.
using var legacyDatabase = new SqliteConnection("Data Source=:memory:");
legacyDatabase.Open();
var legacyOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(legacyDatabase).Options;
using var legacyContext = new AppDbContext(legacyOptions);
legacyContext.Database.EnsureCreated();
legacyContext.Users.Add(new User
{
    Username = "admin",
    DisplayName = "Legacy Admin",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
    IsAdmin = true,
    CanAccessAdminConsole = true,
    ConsoleRole = ConsoleRole.Admin
});
legacyContext.SaveChanges();
legacyContext.SystemSettings.Add(new SystemSetting { Key = "DemoSeedVersion", Value = "2" });
legacyContext.SaveChanges();
var oldStamp = legacyContext.Users.Single().SecurityStamp;
DbInitializer.Initialize(legacyContext);
Check(legacyContext.Users.Count() == 1 && !legacyContext.Machines.Any(), "Legacy upgrade never creates demo accounts or machines");
Check(!BCrypt.Net.BCrypt.Verify("Admin123!", legacyContext.Users.Single().PasswordHash), "Legacy known password is quarantined");
Check(!legacyContext.Users.Single().CanAccessAdminConsole && legacyContext.Users.Single().SecurityStamp != oldStamp, "Quarantine revokes console access and existing sessions");
BootstrapAdministrator.Reset(legacyContext, "admin", "New-unique-admin-password!");
DbInitializer.Initialize(legacyContext);
Check(BCrypt.Net.BCrypt.Verify("New-unique-admin-password!", legacyContext.Users.Single().PasswordHash) && legacyContext.Users.Single().CanAccessAdminConsole, "Explicit recovery survives restart without re-seeding");

var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ActiveDirectory:Enabled"] = "false",
    ["ActiveDirectory:UseMachineCredentials"] = "true",
    ["Jwt:Key"] = "regression-only-key-with-more-than-32-bytes",
    ["Jwt:Issuer"] = "BeRDP.Backend",
    ["Jwt:Audience"] = "BeRDP.Apps"
}).Build();
var directoryConfig = new ActiveDirectoryConfigurationService(context, configuration, new EphemeralDataProtectionProvider());
await directoryConfig.SaveAsync(new ActiveDirectoryConfiguration
{
    Enabled = true,
    DomainName = "corp.example",
    UseMachineCredentials = false,
    BindUsername = "bind@corp.example",
    BindPassword = " pass with spaces "
}, CancellationToken.None);
var saved = await directoryConfig.GetAsync(CancellationToken.None);
Check(saved.BindPassword == " pass with spaces ", "Bind password preserves leading/trailing spaces");
Check(context.SystemSettings.Single(setting => setting.Key == "ActiveDirectory.BindPassword")
    .Value.StartsWith("protected:v1:"), "Stored bind password is protected");

var user = context.Users.Single(u => u.Username == "admin");
var principal = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
    new Claim("security_stamp", user.SecurityStamp),
    new Claim(ClaimTypes.Role, "Admin")
], "regression"));
var validator = new SessionTokenValidator(context, new UserAuthorizationService(context));
Check(await validator.ValidateAsync(principal, CancellationToken.None), "Current security stamp validates");
user.IsAdmin = false;
context.SaveChanges();
Check(await validator.ValidateAsync(principal, CancellationToken.None) && !principal.IsInRole("Admin"),
    "Admin removal changes role on the next request");
user.SecurityStamp = Guid.NewGuid().ToString("N");
context.SaveChanges();
Check(!await validator.ValidateAsync(principal, CancellationToken.None), "Changed security stamp revokes old session");

var directorySessionUser = new User { Username = "ad-validation", IsActiveDirectoryUser = true,
    DomainName = "corp.example", UserPrincipalName = "ad-validation@corp.example" };
context.Users.Add(directorySessionUser);
context.SaveChanges();
var directoryPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.NameIdentifier, directorySessionUser.Id.ToString()),
    new Claim("security_stamp", directorySessionUser.SecurityStamp),
    new Claim("ad_group", "revoked-group")
], "regression"));
var liveDirectory = new RegressionDirectoryService { Groups = ["current-group"] };
var directoryValidator = new SessionTokenValidator(context, new UserAuthorizationService(context), directoryConfig, liveDirectory);
Check(await directoryValidator.ValidateAsync(directoryPrincipal, CancellationToken.None) &&
    directoryPrincipal.HasClaim("ad_group", "current-group") && !directoryPrincipal.HasClaim("ad_group", "revoked-group"),
    "AD session replaces stale token groups with current directory membership");
liveDirectory.Groups = [];
Check(await directoryValidator.ValidateAsync(directoryPrincipal, CancellationToken.None) &&
    !directoryPrincipal.HasClaim(c => c.Type == "ad_group"), "AD group removal takes effect on the next request");
liveDirectory.Unavailable = true;
Check(!await directoryValidator.ValidateAsync(directoryPrincipal, CancellationToken.None), "Directory failure or disabled account fails closed");
context.Users.Remove(directorySessionUser);
context.SaveChanges();

RdpMonitorViewModel Monitor(int id, string device, int x, int width, bool primary = false) => new()
{
    Id = id, DeviceName = device, Left = x, Top = 0, Width = width, Height = 1080, IsPrimary = primary,
    WorkLeft = x, WorkTop = 0, WorkWidth = width, WorkHeight = 1040
};
IReadOnlyList<RdpMonitorViewModel> monitors = [
    Monitor(0, "left", -1920, 1920), Monitor(1, "middle", 0, 1920, true), Monitor(2, "right", 1920, 2560)
];
var displayOptions = new MachineRdpLocalResourceOptions { SelectedMonitorDevices = ["middle", "right"] };
var display = RdpDisplaySettings.Build(displayOptions, monitors);
Check(display.Contains("selectedmonitors:s:1,2") && display.Contains("use multimon:i:1"),
    "Three-screen setup uses only the selected two displays");
displayOptions.SelectedMonitorDevices = ["left"];
Check(RdpDisplaySettings.Build(displayOptions, monitors).Contains("selectedmonitors:s:0"),
    "A non-primary single display can be selected");
displayOptions.SelectedMonitorDevices = ["left", "right"];
ExpectInvalid(() => RdpDisplaySettings.Build(displayOptions, monitors), "Non-contiguous display selection is rejected");
displayOptions.SelectedMonitorDevices = ["unplugged"];
ExpectInvalid(() => RdpDisplaySettings.Build(displayOptions, monitors), "Disconnected display is rejected");
displayOptions.SelectedMonitorDevices = ["right", "middle"];
Check(RdpDisplaySettings.Build(displayOptions, monitors).Contains("selectedmonitors:s:2,1"),
    "First selected display becomes the remote primary");
displayOptions.SelectedMonitorDevices = ["right"];
displayOptions.UseCustomResolution = true;
displayOptions.DesktopWidth = 1280;
displayOptions.DesktopHeight = 720;
display = RdpDisplaySettings.Build(displayOptions, monitors);
Check(display.Contains("screen mode id:i:1") && display.Contains("desktopwidth:i:1280") &&
    display.Contains("winposstr:s:0,1,1920,0"), "Custom resolution opens on the selected physical display");
displayOptions.DesktopWidth = 199;
ExpectInvalid(() => RdpDisplaySettings.Build(displayOptions, monitors), "Resolution below 200 is rejected");
displayOptions.DesktopWidth = 8193;
ExpectInvalid(() => RdpDisplaySettings.Build(displayOptions, monitors), "Resolution above 8192 is rejected");
displayOptions.DesktopWidth = 200;
displayOptions.DesktopHeight = 200;
Check(RdpDisplaySettings.Build(displayOptions, monitors).Contains("desktopwidth:i:200"), "Resolution lower boundary 200 is supported");
displayOptions.DesktopWidth = 8192;
displayOptions.DesktopHeight = 8192;
Check(RdpDisplaySettings.Build(displayOptions, monitors).Contains("desktopheight:i:8192"), "Resolution upper boundary 8192 is supported");
displayOptions.DesktopWidth = 1280;
displayOptions.DesktopHeight = 720;
displayOptions.UseAllMonitors = true;
ExpectInvalid(() => RdpDisplaySettings.Build(displayOptions, monitors), "Custom resolution with all displays is rejected");
displayOptions.UseAllMonitors = false;
var clone = displayOptions.Clone();
clone.SelectedMonitorDevices.Clear();
Check(displayOptions.SelectedMonitorDevices.Count == 1, "Saved options clone is independent");
Check(RdpProfileWriter.Endpoint("2001:db8::1", 3390) == "[2001:db8::1]:3390", "IPv6 RDP endpoint is bracketed");
var profile = RdpProfileWriter.Build("192.0.2.1", 3389, @"CORP\test", displayOptions, monitors);
Check(profile.Contains("session bpp:i:32") &&
    profile.Contains("redirectclipboard:i:1"), "RDP profile keeps modern graphics and resource redirects");
Check(profile.Contains("connection type:i:3") && profile.Contains("networkautodetect:i:0") &&
    profile.Contains("bandwidthautodetect:i:0"),
    "RDP Experience selects the Satellite connection speed with auto detection off");
Check(profile.Contains("allow font smoothing:i:1") && profile.Contains("allow desktop composition:i:1") &&
    profile.Contains("disable themes:i:0"),
    "RDP Experience enables font smoothing, desktop composition and visual styles");
Check(profile.Contains("disable wallpaper:i:1") && profile.Contains("disable full window drag:i:1") &&
    profile.Contains("disable menu anims:i:1") && profile.Contains("bitmapcachepersistenable:i:0") &&
    profile.Contains("autoreconnection enabled:i:0"),
    "RDP Experience disables every other performance option");
Check(!profile.Contains("bitmapcachepersistenable:i:1") &&
    !profile.Contains("autoreconnection enabled:i:1") &&
    !profile.Contains("networkautodetect:i:1"),
    "RDP profile never re-enables a disabled Experience option");
ExpectInvalid(() => RdpProfileWriter.Build("192.0.2.1", 3389, "test\r\ninjected", displayOptions, monitors),
    "RDP username cannot inject settings");
Check(BackendAddressStore.Validate("https://server:5001").AbsoluteUri == "https://server:5001/",
    "Backend address is normalized");
ExpectInvalid(() => BackendAddressStore.Validate("file:///C:/test"), "Non-HTTP backend address is rejected");
ExpectInvalid(() => BackendAddressStore.Validate("http://user:password@server"), "Credentials embedded in URL are rejected");
var portalConfig = "window.__APP_CONFIG__ = { backendBaseUrl: \"http://localhost:5001\" };";
Check(BackendEndpointResolver.ParseConfigScript(new Uri("http://localhost:5173/"), portalConfig)?.AbsoluteUri ==
      "http://localhost:5001/",
    "Client reads the API address from the admin console config.js");
ExpectInvalid(() => BackendEndpointResolver.ParseConfigScript(new Uri("https://server:5173/"), portalConfig),
    "HTTPS portal cannot downgrade to HTTP API");
ExpectInvalid(() => BackendAddressStore.Validate("http://server:5001"), "Remote cleartext API rejected");
ExpectInvalid(() => BackendEndpointResolver.ParseConfigScript(new Uri("https://server/"),
    "window.__APP_CONFIG__ = { backendBaseUrl: \"https://evil.example\" };"), "Discovery cannot change trusted host");
Check(BackendEndpointResolver.ParseConfigScript(new Uri("https://server:5001/"), "<html></html>") is null,
    "Non-portal config responses are ignored");
Check(BackendEndpointResolver.MapLocalPortalPort(new Uri("http://localhost:5173/"))?.AbsoluteUri ==
      "http://localhost:5001/",
    "Local admin console port maps to the backend API port");
Check(BackendEndpointResolver.MapLocalPortalPort(new Uri("http://127.0.0.1:5173/"))?.Port == 5001,
    "Loopback IP console addresses are mapped as well");
Check(BackendEndpointResolver.MapLocalPortalPort(new Uri("https://portal.example:5173/")) is null,
    "Remote portal addresses are never remapped to a different port");
var resolvedPortal = await BackendEndpointResolver.ResolveAsync(
    "http://localhost:5173",
    (_, _) => Task.FromResult<Uri?>(null),
    CancellationToken.None);
Check(resolvedPortal.WasCorrected && resolvedPortal.Address.Port == 5001 &&
      !string.IsNullOrWhiteSpace(resolvedPortal.Notice),
    "A console URL is corrected to the API before login");
var resolvedApi = await BackendEndpointResolver.ResolveAsync(
    "https://server:5001",
    (_, _) => Task.FromResult<Uri?>(null),
    CancellationToken.None);
Check(!resolvedApi.WasCorrected && resolvedApi.Notice is null,
    "A valid backend address is left unchanged");
var ssoProfile = RdpProfileWriter.Build("vm01.corp.example", 3389, "upn@corp.example", displayOptions, monitors, true);
var promptProfile = RdpProfileWriter.Build("vm01.corp.example", 3389, @"CORP\sam123", displayOptions, monitors, false, true);
Check(promptProfile.Contains("prompt for credentials:i:1") &&
    promptProfile.Contains(@"username:s:CORP\sam123") &&
    promptProfile.Contains("redirectsmartcards:i:0") &&
    promptProfile.Contains("redirectwebauthn:i:0"),
    "Prompt profile instructs mstsc to prompt domain credentials and blocks Windows Hello PIN hijacking");
Check(ssoProfile.Contains("full address:s:vm01.corp.example") &&
    ssoProfile.Contains("enablecredsspsupport:i:1") && !ssoProfile.Contains("password 51"),
    "SSO profile keeps DNS and NLA without embedding a password");
ExpectInvalid(() => RdpProfileWriter.Build("192.0.2.1", 3389, "upn@corp.example", displayOptions, monitors, true),
    "SSO rejects IPv4 instead of falling back to NTLM");
ExpectInvalid(() => RdpProfileWriter.Build("2001:db8::1", 3389, "upn@corp.example", displayOptions, monitors, true),
    "SSO rejects IPv6");
ExpectInvalid(() => RdpProfileWriter.Build("localhost", 3389, "upn@corp.example", displayOptions, monitors, true),
    "SSO rejects localhost");
var directoryUser = new ActiveDirectoryDirectoryUser
{
    SamAccountName = "sam123", DomainName = "corp.example", DisplayName = "Domain Test",
    UserPrincipalName = "different.upn@staff.example", Sid = "S-1-5-21-1-2-3-1001"
};
var imported = new BeRdp.Backend.Models.User
{
    Username = @"corp.example\sam123", DomainName = "CORP.EXAMPLE", IsActiveDirectoryUser = true,
    UserPrincipalName = "DIFFERENT.UPN@STAFF.EXAMPLE"
};
Check(ActiveDirectoryIdentity.Matches(imported, directoryUser),
    "SID-resolved AD identity matches UPN even when UPN prefix and SAM differ");
imported.UserPrincipalName = "wrong@staff.example";
Check(!ActiveDirectoryIdentity.Matches(imported, directoryUser), "Conflicting imported UPN is rejected");
imported.UserPrincipalName = null;
Check(ActiveDirectoryIdentity.Matches(imported, directoryUser), "Legacy canonical SAM import can match without UPN");
imported.DomainName = "CORP";
Check(ActiveDirectoryIdentity.Matches(imported, directoryUser), "Windows SSO accepts a stored NetBIOS domain alias");
imported.DomainName = "untrusted.example";
Check(!ActiveDirectoryIdentity.Matches(imported, directoryUser), "Same SAM in another domain is rejected");
imported.DomainName = "corp.example";
imported.IsActiveDirectoryUser = false;
Check(!ActiveDirectoryIdentity.Matches(imported, directoryUser), "Local accounts never match a Windows SSO identity");

using var services = new ServiceCollection().AddLogging().AddSignalR().Services.BuildServiceProvider();
var hubContext = services.GetRequiredService<IHubContext<AssignmentsHub>>();
var authService = new UserAuthorizationService(context);
var machinesController = new MachinesController(context, hubContext, new RegressionAvailability())
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("ConsoleRole", "Admin")
            ], "regression"))
        }
    }
};
var machineRequest = new CreateMachineRequest
{
    Name = "Regression VM", IpAddress = "192.0.2.20", RdpHostName = "vm01.corp.example",
    EntitledUserIds = [user.Id],
    EntitledAdGroups = [new() { GroupName = "vdi", DomainName = "corp.example" }]
};
var created = await machinesController.CreateMachine(machineRequest, CancellationToken.None);
Check(created.Result is CreatedResult { Value: MachineResponse }, "Machine creation returns the expected API shape");
var machineId = ((MachineResponse)((CreatedResult)created.Result!).Value!).Id;
machineRequest.RdpHostName = "vm02.corp.example";
var updated = await machinesController.UpdateMachine(machineId, machineRequest, CancellationToken.None);
Check(updated.Result is OkObjectResult && context.UserMachines.Count(x => x.MachineId == machineId) == 1 &&
    context.MachineAdGroupEntitlements.Count(x => x.MachineId == machineId) == 1,
    "Updating with retained entitlements does not duplicate composite keys");
Check(context.Machines.Single(x => x.Id == machineId).RdpHostName == "vm02.corp.example",
    "Existing machine DNS name can be changed without recreating it");
context.Machines.Add(new BeRdp.Backend.Models.Machine
{
    Name = "Unassigned VM", IpAddress = "192.0.2.21", RdpPort = 3389
});
context.SaveChanges();
var assignedResult = await machinesController.GetMachines(null, 0, 500, true, CancellationToken.None);
var assignedMachines = (IReadOnlyList<MachineResponse>)((OkObjectResult)assignedResult.Result!).Value!;
// Admin users can see ALL machines even with assignedOnly=true (10 seeded + 2 created in test)
Check(assignedMachines.Count == 1 && assignedMachines.Any(m => m.Id == machineId) && !assignedMachines.Any(m => m.Name == "Unassigned VM"),
    "Desktop clients receive only assigned machines, including administrators");
machinesController.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, "User")
], "regression"));
var standardResult = await machinesController.GetMachines(null, 0, 500, false, CancellationToken.None);
var standardMachines = (IReadOnlyList<MachineResponse>)((OkObjectResult)standardResult.Result!).Value!;
Check(standardMachines.Count == 1,
    "Standard user cannot bypass entitlement filtering with assignedOnly=false");
Check(standardMachines.All(m => m.AssignedUsernames.Count == 0 && m.AssignedUserDisplayNames.Count == 0 &&
        m.AssignedAdGroups.Count == 0 && m.AssignedUserCount == 0),
    "Entitlement-only users do not see who else is assigned to their machines");
var usersController = new UsersController(context);
foreach (var reservedName in new[] { @"corp\impostor", "impostor@corp.example", "x" })
{
    var invalidLocal = await usersController.CreateUser(new CreateUserRequest
    {
        Username = reservedName, DisplayName = "Impostor", Password = "Long-enough-password-1"
    }, CancellationToken.None);
    Check(invalidLocal.Result is BadRequestObjectResult, $"Local user name '{reservedName}' is rejected");
}
machineRequest.EntitledUserIds.Clear();
await machinesController.UpdateMachine(machineId, machineRequest, CancellationToken.None);
machinesController.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, "User"),
    new Claim("ad_group", @"corp.example\vdi")
], "regression"));
var groupResult = await machinesController.GetMachines(null, 0, 500, false, CancellationToken.None);
Check(((IReadOnlyList<MachineResponse>)((OkObjectResult)groupResult.Result!).Value!).Count == 1,
    "Domain group entitlement grants machine visibility without an individual assignment");
machineRequest.EntitledAdGroups.Clear();
await machinesController.UpdateMachine(machineId, machineRequest, CancellationToken.None);
Check(!context.UserMachines.Any(x => x.MachineId == machineId) &&
    !context.MachineAdGroupEntitlements.Any(x => x.MachineId == machineId),
    "Clearing an access list removes previous user and group grants");
var revokedResult = await machinesController.GetMachines(null, 0, 500, false, CancellationToken.None);
Check(((IReadOnlyList<MachineResponse>)((OkObjectResult)revokedResult.Result!).Value!).Count == 0,
    "Removed machine group entitlement disappears even for a token with the old group claim");
var gatewaySettings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["BrowserRdp:Enabled"] = "true",
    ["BrowserRdp:BaseUrl"] = "https://gateway.example.test/guacamole",
    ["BrowserRdp:JsonSecretKey"] = "00112233445566778899aabbccddeeff"
}).Build();
var gateway = new BrowserRdpGateway(gatewaySettings);
Check(await machinesController.CreateBrowserSession(machineId, gateway, CancellationToken.None) is ForbidResult,
    "Local account cannot request a browser RDP ticket");
user.IsActiveDirectoryUser = true;
context.SaveChanges();
Check(await machinesController.CreateBrowserSession(machineId, gateway, CancellationToken.None) is NotFoundObjectResult,
    "Removed entitlement prevents browser RDP launch even if the machine ID is known");
context.UserMachines.Add(new BeRdp.Backend.Models.UserMachine { UserId = user.Id, MachineId = machineId });
context.SaveChanges();
Check(await machinesController.CreateBrowserSession(machineId, gateway, CancellationToken.None) is OkObjectResult,
    "Assigned domain account can request a browser RDP ticket");
gatewaySettings["BrowserRdp:Enabled"] = "false";
Check(await machinesController.CreateBrowserSession(machineId, gateway, CancellationToken.None)
    is ObjectResult { StatusCode: 503 }, "Missing gateway is an explicit service-unavailable error");
gatewaySettings["BrowserRdp:Enabled"] = "true";
var now = DateTimeOffset.UtcNow;
var launchUser = new BeRdp.Backend.Models.User
{
    Username = @"corp.example\sam", UserPrincipalName = "different.upn@staff.example", IsActiveDirectoryUser = true
};
var launchMachine = new BeRdp.Backend.Models.Machine
{
    Name = "Browser VM", IpAddress = "192.0.2.30", RdpHostName = "vm30.corp.example", RdpPort = 3390
};
var launchUrl = new Uri(gateway.CreateLaunchUrl(launchUser, launchMachine, now));
var encrypted = Convert.FromBase64String(Uri.UnescapeDataString(launchUrl.Query["?data=".Length..]));
var jsonKey = Convert.FromHexString(gatewaySettings["BrowserRdp:JsonSecretKey"]!);
using var cipher = Aes.Create();
cipher.Key = jsonKey;
var signed = cipher.DecryptCbc(encrypted, new byte[16], PaddingMode.PKCS7);
Check(CryptographicOperations.FixedTimeEquals(signed.AsSpan(0, 32), HMACSHA256.HashData(jsonKey, signed.AsSpan(32))),
    "Guacamole ticket has a valid HMAC-SHA256 prefix inside AES-CBC");
using var ticket = JsonDocument.Parse(signed.AsMemory(32));
var ticketConnection = ticket.RootElement.GetProperty("connections").GetProperty("Browser VM").GetProperty("parameters");
Check(ticket.RootElement.GetProperty("expires").GetInt64() == now.AddSeconds(60).ToUnixTimeMilliseconds(),
    "Gateway authorization expires after 60 seconds");
Check(ticketConnection.GetProperty("hostname").GetString() == "vm30.corp.example" &&
    ticketConnection.GetProperty("port").GetString() == "3390" &&
    ticketConnection.GetProperty("username").GetString() == launchUser.UserPrincipalName &&
    !ticketConnection.TryGetProperty("password", out _) &&
    ticketConnection.GetProperty("ignore-cert").GetString() == "false",
    "Gateway ticket contains only the assigned endpoint and UPN, no password or certificate bypass");
Check(ticket.RootElement.GetProperty("connections").EnumerateObject().Count() == 1,
    "A browser ticket cannot enumerate or connect to other machines");
gatewaySettings["BrowserRdp:BaseUrl"] = "http://gateway.example.test";
ExpectInvalid(() => gateway.CreateLaunchUrl(launchUser, launchMachine, now), "Non-TLS browser gateway is rejected");
gatewaySettings["BrowserRdp:BaseUrl"] = "https://gateway.example.test";
gatewaySettings["BrowserRdp:JsonSecretKey"] = "invalid";
ExpectInvalid(() => gateway.CreateLaunchUrl(launchUser, launchMachine, now), "Malformed gateway key is rejected");

// ---- Login identity matrix: plain SAM, DOMAIN\user and UPN ----
user.IsActiveDirectoryUser = false;
user.IsAdmin = true;
context.Users.Add(new User
{
    Username = "shared01",
    DisplayName = "Shared Local",
    PasswordHash = BCrypt.Net.BCrypt.HashPassword("SharedPass123!"),
    CanAccessAdminConsole = false
});
context.Users.AddRange(
    new User { Username = @"corp.example\aduser", DisplayName = "AD User", IsActiveDirectoryUser = true, DomainName = "corp.example", UserPrincipalName = "aduser@corp.example" },
    new User { Username = @"corp.example\noupn", DisplayName = "AD Without UPN", IsActiveDirectoryUser = true, DomainName = "corp.example" },
    new User { Username = "upnonly@corp.example", DisplayName = "UPN Only", IsActiveDirectoryUser = true },
    new User { Username = @"staff.example\otheruser", DisplayName = "Other Domain", IsActiveDirectoryUser = true, DomainName = "staff.example" },
    new User { Username = @"corp.example\dupuser", DisplayName = "Duplicate One", IsActiveDirectoryUser = true, DomainName = "corp.example" },
    new User { Username = @"staff.example\dupuser", DisplayName = "Duplicate Two", IsActiveDirectoryUser = true, DomainName = "staff.example" },
    new User { Username = @"corp.example\shared01", DisplayName = "Shared AD", IsActiveDirectoryUser = true, DomainName = "corp.example" });
context.SaveChanges();

var authController = new AuthController(
    context,
    new JwtTokenService(configuration),
    directoryConfig,
    new RegressionDirectoryService(),
    new UserAuthorizationService(context),
    NullLogger<AuthController>.Instance,
    configuration)
{
    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
};

async Task<ActionResult<BackendLoginResponse>> LoginAsync(string identifier, string password)
    => await authController.Login(new LoginRequest { Username = identifier, Password = password }, CancellationToken.None);

async Task<BackendLoginResponse> LoginOkAsync(string identifier, string password, string message)
{
    var result = await LoginAsync(identifier, password);
    if (result.Result is not OkObjectResult { Value: BackendLoginResponse response })
    {
        throw new InvalidOperationException($"{message}: login returned {result.Result?.GetType().Name ?? "null"}");
    }
    Check(true, message);
    return response;
}

async Task LoginRejectedAsync(string identifier, string password, int statusCode, string message)
{
    var result = await LoginAsync(identifier, password);
    var actual = result.Result switch
    {
        UnauthorizedObjectResult => StatusCodes.Status401Unauthorized,
        BadRequestObjectResult => StatusCodes.Status400BadRequest,
        _ => 0
    };
    Check(actual == statusCode, $"{message} (expected {statusCode}, got {actual})");
}

var user01Login = await LoginOkAsync("user01", "User01Pass123!", "Entitlement user01 can log in to the desktop client");
Check(!user01Login.CanAccessAdminConsole && user01Login.ConsoleRole == ConsoleRole.Standard &&
      !string.IsNullOrWhiteSpace(user01Login.Token),
    "user01 receives a token without admin console access");
var user01Token = new JwtSecurityTokenHandler().ReadJwtToken(user01Login.Token);
Check(user01Token.Claims.Any(c => c.Type == "username" && c.Value == "user01") &&
      user01Token.Claims.Any(c => c.Type == "CanAccessAdminConsole" && c.Value == "false") &&
      user01Token.Claims.Any(c => c.Type == "ConsoleRole" && c.Value == "Standard"),
    "user01 token carries the console access claims used by the clients");
var user01Entity = context.Users.Single(x => x.Username == "user01");
var consoleClaimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
    new Claim(ClaimTypes.NameIdentifier, user01Entity.Id.ToString()),
    new Claim("security_stamp", user01Entity.SecurityStamp),
    new Claim("CanAccessAdminConsole", "false"),
    new Claim("ConsoleRole", "Standard")
], "regression"));
user01Entity.CanAccessAdminConsole = true;
user01Entity.ConsoleRole = ConsoleRole.Admin;
context.SaveChanges();
Check(await validator.ValidateAsync(consoleClaimsPrincipal, CancellationToken.None) &&
      consoleClaimsPrincipal.FindFirstValue("CanAccessAdminConsole") == "true" &&
      consoleClaimsPrincipal.FindFirstValue("ConsoleRole") == "Admin",
    "Granting console access is reflected on the next authenticated request");
user01Entity.CanAccessAdminConsole = false;
user01Entity.ConsoleRole = ConsoleRole.Standard;
context.SaveChanges();
Check(await validator.ValidateAsync(consoleClaimsPrincipal, CancellationToken.None) &&
      consoleClaimsPrincipal.FindFirstValue("CanAccessAdminConsole") == "false" &&
      consoleClaimsPrincipal.FindFirstValue("ConsoleRole") == "Standard",
    "Revoking console access is enforced on the next authenticated request");
await LoginOkAsync(@"corp\user01", "User01Pass123!", "user01 can log in as DOMAIN\\user");
await LoginOkAsync(@"CORP\USER01", "User01Pass123!", "Domain-qualified login is case-insensitive");
await LoginOkAsync("user01@corp.example", "User01Pass123!", "user01 can log in with its corporate UPN suffix");
await LoginOkAsync("USER01@CORP.EXAMPLE", "User01Pass123!", "UPN login is case-insensitive");
await LoginOkAsync("user01@corp", "User01Pass123!", "UPN login accepts the NetBIOS domain alias");

var consoleLogin = await LoginOkAsync("console-user@corp.example", "ConsoleUser123!",
    "Read-only console account logs in with a UPN suffix");
Check(consoleLogin.CanAccessAdminConsole && consoleLogin.ConsoleRole == ConsoleRole.Standard,
    "console-user keeps read-only admin console access");
var adminLogin = await LoginOkAsync("admin", "Berdp@Admin2026!Secure#Password", "Admin logs in with a plain username");
Check(adminLogin.IsAdmin && adminLogin.CanAccessAdminConsole && adminLogin.ConsoleRole == ConsoleRole.Admin,
    "Admin keeps full admin console access and the effective admin role");
await LoginOkAsync("admin@corp.example", "Berdp@Admin2026!Secure#Password", "Admin logs in with a UPN suffix");

var adLogin = await LoginOkAsync("aduser", "AdPass123!", "Imported AD user logs in with a plain SAM");
Check(adLogin.IsActiveDirectoryUser && adLogin.Username == @"corp.example\aduser",
    "AD login returns the canonical imported identity");
await LoginOkAsync(@"corp\aduser", "AdPass123!", "AD user logs in with the NetBIOS domain prefix");
await LoginOkAsync(@"corp.example\aduser", "AdPass123!", "AD user logs in with the DNS domain prefix");
await LoginOkAsync("aduser@corp.example", "AdPass123!", "AD user logs in with the stored UPN");
await LoginOkAsync("aduser@CORP", "AdPass123!", "AD user logs in with a NetBIOS UPN suffix");
await LoginOkAsync("noupn@corp.example", "AdPass123!", "AD user without a stored UPN resolves through its domain");
Check((await authController.Login(new LoginRequest { Username = "otheruser@staff.example", Password = "AdPass123!" }, CancellationToken.None)).Result is UnauthorizedObjectResult, "Foreign directory account cannot authenticate against configured domain");
await LoginOkAsync("upnonly", "AdPass123!", "UPN-only AD account resolves with a plain SAM");
await LoginOkAsync(@"CORP\upnonly", "AdPass123!", "UPN-only AD account resolves with the NetBIOS domain prefix");
await LoginOkAsync(@"corp.example\upnonly", "AdPass123!", "UPN-only AD account resolves with the DNS domain prefix");
await LoginOkAsync("upnonly@CORP", "AdPass123!", "UPN-only AD account resolves with the NetBIOS UPN suffix");

await LoginRejectedAsync("otheruser@corp.example", "AdPass123!", 401,
    "A different FQDN is not treated as the same domain");
await LoginRejectedAsync("upnonly@staff.example", "AdPass123!", 401,
    "A UPN-only AD account rejects a different FQDN suffix");
await LoginRejectedAsync(@"staff.example\aduser", "AdPass123!", 401,
    "A wrong domain prefix does not match another domain's AD account");
await LoginRejectedAsync("dupuser", "AdPass123!", 400,
    "An ambiguous plain SAM returns guidance instead of guessing a domain");
await LoginOkAsync(@"corp\dupuser", "AdPass123!", "An ambiguous SAM resolves with the NetBIOS domain prefix");
await LoginRejectedAsync(@"staff.example\dupuser", "AdPass123!", 401, "Foreign-domain duplicate SAM cannot authenticate against configured directory");
await LoginRejectedAsync("user01", "WrongPassword!", 401, "Wrong password is rejected for a local entitlement user");
await LoginRejectedAsync("aduser", "WrongPassword!", 401, "Wrong password is rejected for an AD user");
await LoginRejectedAsync("unknown-user", "User01Pass123!", 401, "Unknown identifier returns the generic credential error");

var sharedLocal = await LoginOkAsync("shared01", "SharedPass123!", "Plain SAM prefers the exact local account");
Check(!sharedLocal.IsActiveDirectoryUser, "Exact local username wins over a domain-prefixed AD username");
var sharedAd = await LoginOkAsync(@"corp\shared01", "AdPass123!", "Domain-qualified SAM resolves to the AD account");
Check(sharedAd.IsActiveDirectoryUser, "Domain-qualified login uses the AD account when one exists");

using (var verifyContext = new AppDbContext(dbOptions))
{
    Check(verifyContext.Users.Single(x => x.Username == "user01").LastLoginAtUtc is not null,
        "Successful login records LastLoginAtUtc in the database");
}

var usernameMaxLength = typeof(LoginRequest).GetProperty(nameof(LoginRequest.Username))!
    .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.MaxLengthAttribute), true)
    .Cast<System.ComponentModel.DataAnnotations.MaxLengthAttribute>()
    .Single().Length;
Check(usernameMaxLength >= 256, "Login accepts long UPN/domain-qualified identifiers");

Console.WriteLine($"SUCCESS: {passed} regression checks passed.");

sealed class RegressionDirectoryService : IActiveDirectoryService
{
    private const string Password = "AdPass123!";
    public IReadOnlyList<string> Groups { get; set; } = [];
    public bool Unavailable { get; set; }

    public Task<ActiveDirectoryDirectoryUser> GetUserBySidAsync(
        string sid,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("SID lookup is not used in the login matrix.");

    public Task TestConnectionAsync(ActiveDirectoryConfiguration configuration, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task<bool> ValidateCredentialsAsync(
        string username,
        string password,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
        => Task.FromResult(password == Password);

    public Task<IReadOnlyList<ActiveDirectoryDirectoryUser>> SearchUsersAsync(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ActiveDirectoryDirectoryUser>>([]);

    public Task<IReadOnlyList<ActiveDirectoryDirectoryGroup>> SearchGroupsAsync(
        string query,
        int limit,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ActiveDirectoryDirectoryGroup>>([]);

    public Task<IReadOnlyList<string>> GetUserGroupNamesAsync(
        string username,
        ActiveDirectoryConfiguration configuration,
        CancellationToken cancellationToken)
        => Unavailable ? throw new InvalidOperationException("Directory unavailable or account disabled")
            : Task.FromResult(Groups);
}

sealed class RegressionAvailability : IRdpAvailabilityService
{
    public Task<bool> IsAvailableAsync(string ipAddress, int port, CancellationToken cancellationToken)
        => Task.FromResult(true);
}
