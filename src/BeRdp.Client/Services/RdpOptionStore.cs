using System.IO;
using System.Diagnostics;
using System.Text.Json;
using BeRdp.Client.Models;

namespace BeRdp.Client.Services;

public sealed class RdpOptionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _filePath;
    private Dictionary<string, MachineRdpLocalResourceOptions> _machineOptions;
    public string? LoadWarning { get; private set; }

    public RdpOptionStore()
    {
        _filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BeRDP",
            "Client",
            "rdp-options.json");
        _machineOptions = Load();
    }

    public MachineRdpLocalResourceOptions Get(Guid machineId)
    {
        lock (_sync)
        {
            var key = NormalizeKey(machineId);
            if (_machineOptions.TryGetValue(key, out var existing))
            {
                return existing.Clone();
            }

            return new MachineRdpLocalResourceOptions();
        }
    }

    public void Save(Guid machineId, MachineRdpLocalResourceOptions options)
    {
        lock (_sync)
        {
            var key = NormalizeKey(machineId);
            var updated = new Dictionary<string, MachineRdpLocalResourceOptions>(_machineOptions, StringComparer.OrdinalIgnoreCase)
            {
                [key] = options.Clone()
            };
            Persist(updated);
            _machineOptions = updated;
        }
    }

    private Dictionary<string, MachineRdpLocalResourceOptions> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, MachineRdpLocalResourceOptions>(StringComparer.OrdinalIgnoreCase);
            }

            var rawJson = File.ReadAllText(_filePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, MachineRdpLocalResourceOptions>>(rawJson, JsonOptions);
            if (parsed is null || parsed.Values.Any(value => value is null))
            {
                throw new JsonException("RDP ayar dosyası boş veya null makine ayarları içeriyor.");
            }
            return new Dictionary<string, MachineRdpLocalResourceOptions>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            var backupPath = _filePath + ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Move(_filePath, backupPath);
            LoadWarning = $"Geçersiz RDP ayarları {backupPath} dosyasında korundu. Ekran ayarlarını yeniden seçin.";
            Trace.TraceWarning($"{LoadWarning} {ex.Message}");
            return new Dictionary<string, MachineRdpLocalResourceOptions>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist(Dictionary<string, MachineRdpLocalResourceOptions> options)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(options, JsonOptions);
        var temporaryPath = _filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string NormalizeKey(Guid machineId)
    {
        return machineId.ToString("N");
    }
}
