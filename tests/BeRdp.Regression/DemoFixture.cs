using Microsoft.EntityFrameworkCore;
using BeRdp.Backend.Data;
using BeRdp.Backend.Models;

internal static class DemoFixture
{
    private const string AdminPassword = "Berdp@Admin2026!Secure#Password";
    private const string LegacyAdminPassword = "Admin123!";
    private const string StandardConsolePassword = "ConsoleUser123!";
    private const string SeedVersionKey = "DemoSeedVersion";
    private const int CurrentSeedVersion = 2;

    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();
        Seed(context);
    }

    public static void Seed(AppDbContext context)
    {
        var seedVersionSetting = context.SystemSettings.FirstOrDefault(x => x.Key == SeedVersionKey);
        var seedVersion = 0;
        if (seedVersionSetting is not null)
        {
            int.TryParse(seedVersionSetting.Value, out seedVersion);
        }

        if (seedVersion >= CurrentSeedVersion)
        {
            return;
        }

        // Seed/reconcile the demo accounts. This intentionally runs against existing
        // databases too: older builds only created "admin", so upgrades must add the
        // missing console and entitlement users instead of exiting on Users.Any().
        EnsureLocalUser(context, "admin", "Platform Admin", AdminPassword, LegacyAdminPassword,
            isAdmin: true, canAccessAdminConsole: true, ConsoleRole.Admin);
        EnsureLocalUser(context, "console-user", "Console Read-Only User", StandardConsolePassword, null,
            isAdmin: false, canAccessAdminConsole: true, ConsoleRole.Standard);

        for (var i = 1; i <= 10; i++)
        {
            EnsureLocalUser(context, $"user{i:D2}", $"Test Kullanıcı {i:D2}", $"User{i:D2}Pass123!", null,
                isAdmin: false, canAccessAdminConsole: false, ConsoleRole.Standard);
        }

        context.SaveChanges();

        SeedMachines(context);

        if (seedVersionSetting is null)
        {
            seedVersionSetting = new SystemSetting { Key = SeedVersionKey };
            context.SystemSettings.Add(seedVersionSetting);
        }

        seedVersionSetting.Value = CurrentSeedVersion.ToString();
        context.SaveChanges();
    }

    private static void EnsureLocalUser(
        AppDbContext context,
        string username,
        string displayName,
        string password,
        string? legacyPassword,
        bool isAdmin,
        bool canAccessAdminConsole,
        ConsoleRole consoleRole)
    {
        var user = context.Users.SingleOrDefault(x => x.Username == username);
        if (user is null)
        {
            user = new User
            {
                Username = username,
                DisplayName = displayName,
                IsActiveDirectoryUser = false,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };
            context.Users.Add(user);
        }
        else if (user.IsActiveDirectoryUser)
        {
            // Never overwrite an AD-backed account that happens to share a demo username.
            return;
        }
        else if (string.IsNullOrWhiteSpace(user.PasswordHash) ||
                 (legacyPassword is not null &&
                  !PasswordMatches(user.PasswordHash, password) &&
                  PasswordMatches(user.PasswordHash, legacyPassword)))
        {
            // Repair the legacy bootstrap hash once so the documented password works.
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        }

        user.IsAdmin = isAdmin;
        user.CanAccessAdminConsole = canAccessAdminConsole;
        user.ConsoleRole = consoleRole;
        if (string.IsNullOrWhiteSpace(user.DisplayName))
        {
            user.DisplayName = displayName;
        }
        if (string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }
    }

    private static bool PasswordMatches(string? hash, string password)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    private static void SeedMachines(AppDbContext context)
    {
        var users = context.Users
            .Where(u => u.Username.StartsWith("user"))
            .OrderBy(u => u.Username)
            .ToList();
        if (users.Count == 0)
        {
            return;
        }

        var random = new Random(42);
        for (var i = 1; i <= 10; i++)
        {
            var machineName = $"VM-{i:D3}";
            var machine = context.Machines.FirstOrDefault(x => x.Name == machineName);
            var isNewMachine = machine is null;

            if (machine is null)
            {
                machine = new Machine
                {
                    Name = machineName,
                    IpAddress = BuildUniqueSeedAddress(context, i),
                    RdpHostName = $"vm-{i:D3}.berdp.local",
                    RdpPort = 3389
                };
                context.Machines.Add(machine);
            }

            var assignedUserIds = context.UserMachines
                .Where(x => x.MachineId == machine.Id)
                .Select(x => x.UserId)
                .ToHashSet();

            // A new machine, or a machine left without entitlements by an older build,
            // gets 2-3 demo users.
            if (isNewMachine || assignedUserIds.Count == 0)
            {
                var assignedUsers = users.OrderBy(_ => random.Next()).Take(random.Next(2, 4)).ToList();
                foreach (var user in assignedUsers.Where(user => !assignedUserIds.Contains(user.Id)))
                {
                    context.UserMachines.Add(new UserMachine
                    {
                        UserId = user.Id,
                        MachineId = machine.Id
                    });
                    assignedUserIds.Add(user.Id);
                }
            }

            if (!isNewMachine)
            {
                continue;
            }

            if (i % 2 == 0)
            {
                context.MachineAdGroupEntitlements.Add(new MachineAdGroupEntitlement
                {
                    MachineId = machine.Id,
                    GroupName = "berdp-users",
                    DomainName = "berdp.local"
                });
            }
            if (i % 3 == 0)
            {
                context.MachineAdGroupEntitlements.Add(new MachineAdGroupEntitlement
                {
                    MachineId = machine.Id,
                    GroupName = "berdp-powerusers",
                    DomainName = "berdp.local"
                });
            }
        }

        context.SaveChanges();
    }

    private static string BuildUniqueSeedAddress(AppDbContext context, int machineIndex)
    {
        for (var host = 10; host < 250; host++)
        {
            var candidate = $"10.20.{machineIndex}.{host}";
            if (!context.Machines.Any(x => x.IpAddress == candidate))
            {
                return candidate;
            }
        }

        return $"10.20.{machineIndex}.250";
    }

}
