using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;
using BeRdp.Backend.Data;
using BeRdp.Backend.Models;

namespace BeRdp.Backend.Services;

public static class BootstrapAdministrator
{
    public static void Reset(AppDbContext db, string username, string password)
    {
        if (password.Length < 14 || Encoding.UTF8.GetByteCount(password) > 72 || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Parola en az 14 karakter, en fazla 72 UTF-8 byte olmalıdır.");
        var user = db.Users.SingleOrDefault(x => x.Username == username.Trim().ToLowerInvariant() && x.IsAdmin && !x.IsActiveDirectoryUser)
            ?? throw new InvalidOperationException("Yerel yönetici bulunamadı.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.CanAccessAdminConsole = true;
        user.ConsoleRole = ConsoleRole.Admin;
        db.SaveChanges();
    }

    public static void Create(AppDbContext db, string username, string password)
    {
        username = username.Trim().ToLowerInvariant();
        if (!Regex.IsMatch(username, "^[a-z0-9][a-z0-9._-]{2,63}$"))
            throw new InvalidOperationException("Yönetici adı 3–64 harf, rakam, nokta, tire veya alt çizgi içermelidir.");
        if (password.Length < 14 || Encoding.UTF8.GetByteCount(password) > 72 || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Parola en az 14 karakter, UTF-8 olarak en fazla 72 byte olmalıdır.");
        using var transaction = db.Database.BeginTransaction();
        if (db.Users.Any())
            throw new InvalidOperationException("İlk yönetici yalnızca boş kullanıcı veritabanında oluşturulabilir.");
        db.Users.Add(new User { Username = username, DisplayName = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), IsAdmin = true,
            CanAccessAdminConsole = true, ConsoleRole = ConsoleRole.Admin });
        db.SaveChanges();
        transaction.Commit();
    }

    // Server Setup passes the password collected on its wizard page through this
    // process-private variable so it never appears on a command line or in a file.
    public const string SetupPasswordVariable = "BERDP_SETUP_ADMIN_PASSWORD";

    public static string ReadPassword()
    {
        var setupPassword = Environment.GetEnvironmentVariable(SetupPasswordVariable);
        if (!string.IsNullOrEmpty(setupPassword))
        {
            Environment.SetEnvironmentVariable(SetupPasswordVariable, null);
            return setupPassword;
        }
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? throw new InvalidOperationException("Standart girdiden parola okunamadı.");
        Console.Write("Yeni yönetici parolası: ");
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
            else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
        }
    }
}
