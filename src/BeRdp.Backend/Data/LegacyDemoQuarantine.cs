using System.Security.Cryptography;

namespace BeRdp.Backend.Data;

internal static class LegacyDemoQuarantine
{
    // These are compromised *historical* credentials used only to disable unchanged
    // legacy accounts. No account or entitlement is ever created by this migration.
    public static void Apply(AppDbContext db)
    {
        if (!db.SystemSettings.Any(x => x.Key == "DemoSeedVersion") ||
            db.SystemSettings.Any(x => x.Key == "LegacyDemoQuarantined")) return;
        var known = new Dictionary<string, string[]>
        {
            ["admin"] = ["Admin123!", "Berdp@Admin2026!Secure#Password"],
            ["console-user"] = ["ConsoleUser123!"]
        };
        for (var i = 1; i <= 10; i++) known[$"user{i:D2}"] = [$"User{i:D2}Pass123!"];
        foreach (var user in db.Users.Where(x => !x.IsActiveDirectoryUser).ToList())
        {
            if (!known.TryGetValue(user.Username, out var passwords)) continue;
            bool matches;
            try { matches = passwords.Any(p => BCrypt.Net.BCrypt.Verify(p, user.PasswordHash)); }
            catch { matches = false; }
            if (!matches) continue;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.CanAccessAdminConsole = false;
        }
        db.SystemSettings.Add(new Models.SystemSetting { Key = "LegacyDemoQuarantined", Value = "1" });
        db.SaveChanges();
    }
}
