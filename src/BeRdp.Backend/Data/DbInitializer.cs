using System.Data.Common;
using System.Text.RegularExpressions;
using BeRdp.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BeRdp.Backend.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        context.Database.EnsureCreated();
        EnsureSchemaUpgrades(context);
        if (!context.SystemSettings.Any(x => x.Key == "ConsoleRolesMigrated"))
        {
            MigrateExistingUsers(context);
            context.SystemSettings.Add(new SystemSetting { Key = "ConsoleRolesMigrated", Value = "1" });
            context.SaveChanges();
        }
        LegacyDemoQuarantine.Apply(context);
    }

    private static void EnsureSchemaUpgrades(AppDbContext context)
    {
        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS SystemSettings (
                Key TEXT NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
                Value TEXT NOT NULL
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS MachineAdGroupEntitlements (
                Id TEXT NOT NULL CONSTRAINT PK_MachineAdGroupEntitlements PRIMARY KEY,
                MachineId TEXT NOT NULL,
                GroupName TEXT NOT NULL,
                DomainName TEXT NULL,
                CONSTRAINT FK_MachineAdGroupEntitlements_Machines_MachineId FOREIGN KEY (MachineId) REFERENCES Machines (Id) ON DELETE CASCADE
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineAdGroupEntitlements_Machine_Group_Domain
            ON MachineAdGroupEntitlements (MachineId, GroupName, DomainName);
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS AdminAccessGroups (
                Id TEXT NOT NULL CONSTRAINT PK_AdminAccessGroups PRIMARY KEY,
                GroupName TEXT NOT NULL,
                DomainName TEXT NULL,
                CreatedAtUtc TEXT NOT NULL
            );
            """);

        context.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_AdminAccessGroups_Group_Domain
            ON AdminAccessGroups (GroupName, DomainName);
            """);

        AddColumnIfMissing(context, "Users", "IsActiveDirectoryUser", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(context, "Users", "DomainName", "TEXT NULL");
        AddColumnIfMissing(context, "Users", "UserPrincipalName", "TEXT NULL");
        AddColumnIfMissing(context, "Users", "SecurityStamp", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(context, "Users", "CanAccessAdminConsole", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(context, "Users", "ConsoleRole", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(context, "Machines", "RdpHostName", "TEXT NULL");
        context.Database.ExecuteSqlRaw("""
            UPDATE Users SET SecurityStamp = lower(hex(randomblob(16)))
            WHERE SecurityStamp = '';
            """);
    }

    private static void MigrateExistingUsers(AppDbContext context)
    {
        // Check if new columns exist
        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;
        if (shouldCloseConnection) connection.Open();

        bool hasCanAccessAdminConsole = false;
        bool hasConsoleRole = false;

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('Users');";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var colName = reader.GetString(1);
                if (colName == "CanAccessAdminConsole") hasCanAccessAdminConsole = true;
                if (colName == "ConsoleRole") hasConsoleRole = true;
            }
        }
        finally
        {
            if (shouldCloseConnection) connection.Close();
        }

        if (!hasCanAccessAdminConsole || !hasConsoleRole)
        {
            return; // Columns don't exist, will be added by AddColumnIfMissing
        }

        // Update existing users based on their existing roles
        context.Database.ExecuteSqlRaw("""
            UPDATE Users SET
                CanAccessAdminConsole = CASE
                    WHEN IsAdmin = 1 THEN 1
                    ELSE 0
                END,
                ConsoleRole = CASE
                    WHEN IsAdmin = 1 THEN 1
                    ELSE 0
                END
            WHERE CanAccessAdminConsole = 0 AND ConsoleRole = 0;
            """);
    }

    private static void AddColumnIfMissing(AppDbContext context, string tableName, string columnName, string columnDefinition)
    {
        ValidateSqlIdentifier(tableName, nameof(tableName));
        ValidateSqlIdentifier(columnName, nameof(columnName));

        var connection = context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;
        if (shouldCloseConnection)
        {
            connection.Open();
        }

        try
        {
            if (ColumnExists(connection, tableName, columnName))
            {
                return;
            }

            var alterStatement = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDefinition + ";";
            context.Database.ExecuteSqlRaw(alterStatement);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                connection.Close();
            }
        }
    }

    private static bool ColumnExists(DbConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''")}');";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var existingColumnName = reader.GetString(1);
            if (string.Equals(existingColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateSqlIdentifier(string identifier, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be empty.", parameterName);
        }

        if (!Regex.IsMatch(identifier, "^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            throw new InvalidOperationException($"Invalid SQL identifier: {identifier}");
        }
    }
}
