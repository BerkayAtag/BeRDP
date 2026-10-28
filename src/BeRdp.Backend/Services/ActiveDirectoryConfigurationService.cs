using BeRdp.Backend.Data;
using BeRdp.Backend.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BeRdp.Backend.Services;

public class ActiveDirectoryConfigurationService(
    AppDbContext dbContext,
    IConfiguration configuration,
    IDataProtectionProvider dataProtectionProvider) : IActiveDirectoryConfigurationService
{
    private const string ProtectedPasswordPrefix = "protected:v1:";
    private readonly IDataProtector _passwordProtector =
        dataProtectionProvider.CreateProtector("BeRdp.ActiveDirectory.BindPassword.v1");
    private const string KeyEnabled = "ActiveDirectory.Enabled";
    private const string KeyDomainName = "ActiveDirectory.DomainName";
    private const string KeyLdapContainer = "ActiveDirectory.LdapContainer";
    private const string KeyUseMachineCredentials = "ActiveDirectory.UseMachineCredentials";
    private const string KeyBindUsername = "ActiveDirectory.BindUsername";
    private const string KeyBindPassword = "ActiveDirectory.BindPassword";

    private static readonly string[] ManagedKeys =
    {
        KeyEnabled,
        KeyDomainName,
        KeyLdapContainer,
        KeyUseMachineCredentials,
        KeyBindUsername,
        KeyBindPassword
    };

    public async Task<ActiveDirectoryConfiguration> GetAsync(CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("ActiveDirectory");
        var defaults = new ActiveDirectoryConfiguration
        {
            Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
            DomainName = section["DomainName"] ?? string.Empty,
            LdapContainer = section["LdapContainer"],
            UseMachineCredentials = !bool.TryParse(section["UseMachineCredentials"], out var useMachineCredentials) || useMachineCredentials,
            BindUsername = section["BindUsername"],
            BindPassword = section["BindPassword"]
        };

        var overrides = await dbContext.SystemSettings
            .AsNoTracking()
            .Where(x => ManagedKeys.Contains(x.Key))
            .ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);

        if (overrides.TryGetValue(KeyBindPassword, out var legacyPassword) &&
            !string.IsNullOrEmpty(legacyPassword) &&
            !legacyPassword.StartsWith(ProtectedPasswordPrefix, StringComparison.Ordinal))
        {
            await UpsertAsync(KeyBindPassword, ProtectedPasswordPrefix + _passwordProtector.Protect(legacyPassword), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ActiveDirectoryConfiguration
        {
            Enabled = ParseBoolean(overrides, KeyEnabled, defaults.Enabled),
            DomainName = ParseString(overrides, KeyDomainName, defaults.DomainName),
            LdapContainer = ParseNullableString(overrides, KeyLdapContainer, defaults.LdapContainer),
            UseMachineCredentials = ParseBoolean(overrides, KeyUseMachineCredentials, defaults.UseMachineCredentials),
            BindUsername = ParseNullableString(overrides, KeyBindUsername, defaults.BindUsername),
            BindPassword = ReadPassword(overrides.TryGetValue(KeyBindPassword, out var password)
                ? password
                : defaults.BindPassword)
        };
    }

    public async Task<ActiveDirectoryConfiguration> SaveAsync(ActiveDirectoryConfiguration configurationToSave, CancellationToken cancellationToken)
    {
        var normalized = new ActiveDirectoryConfiguration
        {
            Enabled = configurationToSave.Enabled,
            DomainName = configurationToSave.DomainName.Trim(),
            LdapContainer = NormalizeNullable(configurationToSave.LdapContainer),
            UseMachineCredentials = configurationToSave.UseMachineCredentials,
            BindUsername = NormalizeNullable(configurationToSave.BindUsername),
            BindPassword = configurationToSave.BindPassword ?? string.Empty
        };

        if (normalized.UseMachineCredentials)
        {
            normalized.BindUsername = string.Empty;
            normalized.BindPassword = string.Empty;
        }

        await UpsertAsync(KeyEnabled, normalized.Enabled ? "true" : "false", cancellationToken);
        await UpsertAsync(KeyDomainName, normalized.DomainName, cancellationToken);
        await UpsertAsync(KeyLdapContainer, normalized.LdapContainer ?? string.Empty, cancellationToken);
        await UpsertAsync(KeyUseMachineCredentials, normalized.UseMachineCredentials ? "true" : "false", cancellationToken);
        await UpsertAsync(KeyBindUsername, normalized.BindUsername ?? string.Empty, cancellationToken);
        var protectedPassword = string.IsNullOrEmpty(normalized.BindPassword)
            ? string.Empty
            : ProtectedPasswordPrefix + _passwordProtector.Protect(normalized.BindPassword);
        await UpsertAsync(KeyBindPassword, protectedPassword, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return normalized;
    }

    private async Task UpsertAsync(string key, string value, CancellationToken cancellationToken)
    {
        var existing = await dbContext.SystemSettings.SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (existing is null)
        {
            dbContext.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value
            });

            return;
        }

        existing.Value = value;
    }

    private static bool ParseBoolean(IReadOnlyDictionary<string, string> values, string key, bool fallback)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        return bool.TryParse(rawValue, out var parsed) && parsed;
    }

    private static string ParseString(IReadOnlyDictionary<string, string> values, string key, string fallback)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            return fallback;
        }

        return rawValue;
    }

    private static string? ParseNullableString(IReadOnlyDictionary<string, string> values, string key, string? fallback)
    {
        if (!values.TryGetValue(key, out var rawValue))
        {
            return NormalizeNullable(fallback);
        }

        return NormalizeNullable(rawValue);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string? ReadPassword(string? value)
    {
        if (value is null || !value.StartsWith(ProtectedPasswordPrefix, StringComparison.Ordinal))
        {
            return value;
        }

        try
        {
            return _passwordProtector.Unprotect(value[ProtectedPasswordPrefix.Length..]);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "AD bind parolası çözülemiyor. Sunucunun DataProtectionKeys yedeğini geri yükleyin.",
                ex);
        }
    }
}
