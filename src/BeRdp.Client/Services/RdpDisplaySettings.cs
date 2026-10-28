using System.Text;
using BeRdp.Client.Models;

namespace BeRdp.Client.Services;

public static class RdpDisplaySettings
{
    public static IReadOnlyList<RdpMonitorViewModel> Resolve(
        MachineRdpLocalResourceOptions options,
        IReadOnlyList<RdpMonitorViewModel> activeMonitors)
    {
        if (activeMonitors.Count == 0)
        {
            throw new InvalidOperationException("Aktif monitör bulunamadı.");
        }
        if (options.UseAllMonitors)
        {
            return activeMonitors.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Id).ToList();
        }

        var devices = options.SelectedMonitorDevices ?? [];
        var ids = options.SelectedMonitorIds ?? [];
        IReadOnlyList<RdpMonitorViewModel> selected;
        if (devices.Count > 0)
        {
            selected = devices.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(device => activeMonitors.SingleOrDefault(monitor =>
                    string.Equals(monitor.DeviceName, device, StringComparison.OrdinalIgnoreCase)) ??
                    throw new InvalidOperationException($"Seçilen monitör ({device}) bağlı değil. Ekran ayarlarını yeniden seçin."))
                .ToList();
        }
        else if (ids.Count > 0)
        {
            selected = ids.Distinct().Select(id =>
                activeMonitors.SingleOrDefault(monitor => monitor.Id == id) ??
                throw new InvalidOperationException("Kaydedilmiş monitör bağlı değil. Ekran ayarlarını yeniden seçin.")).ToList();
        }
        else
        {
            selected = [activeMonitors.FirstOrDefault(x => x.IsPrimary) ?? activeMonitors[0]];
        }

        ValidateSelection(options, selected);
        return selected;
    }

    public static void ValidateSelection(
        MachineRdpLocalResourceOptions options,
        IReadOnlyList<RdpMonitorViewModel> selected)
    {
        if (selected.Count == 0)
        {
            throw new InvalidOperationException("En az bir monitör seçin.");
        }
        if (options.UseCustomResolution &&
            (options.UseAllMonitors || selected.Count != 1))
        {
            throw new InvalidOperationException("Özel çözünürlük yalnızca tek monitör seçiminde kullanılabilir.");
        }
        if (options.UseCustomResolution &&
            (options.DesktopWidth is < 200 or > 8192 || options.DesktopHeight is < 200 or > 8192))
        {
            throw new InvalidOperationException("Çözünürlük değerleri 200-8192 arasında olmalıdır.");
        }
        if (!options.UseAllMonitors && !IsContiguous(selected))
        {
            throw new InvalidOperationException("Seçilen monitörler bitişik olmalıdır; arada seçilmeyen bir ekran bırakılamaz.");
        }
    }

    public static string Build(
        MachineRdpLocalResourceOptions options,
        IReadOnlyList<RdpMonitorViewModel> activeMonitors)
    {
        var selected = Resolve(options, activeMonitors);
        ValidateSelection(options, selected);
        var settings = new StringBuilder();
        settings.AppendLine($"screen mode id:i:{(options.UseCustomResolution ? 1 : 2)}");
        settings.AppendLine($"use multimon:i:{(options.UseCustomResolution ? 0 : 1)}");
        if (!options.UseCustomResolution && !options.UseAllMonitors)
        {
            settings.AppendLine($"selectedmonitors:s:{string.Join(",", selected.Select(monitor => monitor.Id))}");
        }
        if (options.UseCustomResolution)
        {
            settings.AppendLine($"desktopwidth:i:{options.DesktopWidth}");
            settings.AppendLine($"desktopheight:i:{options.DesktopHeight}");
            settings.AppendLine("dynamic resolution:i:0");
            settings.AppendLine("smart sizing:i:1");
            var monitor = selected[0];
            var right = monitor.WorkLeft + Math.Min(options.DesktopWidth + 16, monitor.WorkWidth);
            var bottom = monitor.WorkTop + Math.Min(options.DesktopHeight + 40, monitor.WorkHeight);
            settings.AppendLine($"winposstr:s:0,1,{monitor.WorkLeft},{monitor.WorkTop},{right},{bottom}");
        }

        return settings.ToString();
    }

    public static bool IsContiguous(IReadOnlyList<RdpMonitorViewModel> monitors)
    {
        if (monitors.Count == 0)
        {
            return false;
        }
        var visited = new HashSet<int> { monitors[0].Id };
        var pending = new Queue<RdpMonitorViewModel>();
        pending.Enqueue(monitors[0]);
        while (pending.TryDequeue(out var current))
        {
            foreach (var candidate in monitors.Where(monitor => !visited.Contains(monitor.Id)))
            {
                var horizontal =
                    (current.Left + current.Width == candidate.Left || candidate.Left + candidate.Width == current.Left) &&
                    Math.Max(current.Top, candidate.Top) < Math.Min(current.Top + current.Height, candidate.Top + candidate.Height);
                var vertical =
                    (current.Top + current.Height == candidate.Top || candidate.Top + candidate.Height == current.Top) &&
                    Math.Max(current.Left, candidate.Left) < Math.Min(current.Left + current.Width, candidate.Left + candidate.Width);
                if (horizontal || vertical)
                {
                    visited.Add(candidate.Id);
                    pending.Enqueue(candidate);
                }
            }
        }

        return visited.Count == monitors.Count;
    }
}
