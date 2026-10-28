using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BeRdp.Client.Services;

public sealed class RdpCredentialStore
{
    private const int NotFound = 1168;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<string>> AcquireAsync(
        IEnumerable<string> targets,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException("RDP için geçerli kullanıcı bilgisi bulunamadı.");
        }
        await _gate.WaitAsync(cancellationToken);
        var acquired = new List<string>();
        try
        {
            foreach (var target in targets.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_entries.TryGetValue(target, out var existing))
                {
                    existing.ReferenceCount++;
                }
                else
                {
                    var original = Read(target);
                    var replacement = new Snapshot(username, Encoding.Unicode.GetBytes(password), 1, 0, null, null, []);
                    try
                    {
                        Write(target, replacement);
                        _entries[target] = new Entry(original) { ReferenceCount = 1 };
                    }
                    catch (Win32Exception)
                    {
                        original?.Clear();
                        throw;
                    }
                    finally
                    {
                        replacement.Clear();
                    }
                }
                acquired.Add(target);
            }
            return acquired;
        }
        catch (Exception ex) when (ex is Win32Exception or OperationCanceledException or InvalidOperationException)
        {
            try
            {
                ReleaseCore(acquired);
            }
            catch (InvalidOperationException cleanupError)
            {
                throw new InvalidOperationException("RDP kimlik bilgileri hazırlanamadı ve geri alma işlemi başarısız oldu.", new AggregateException(ex, cleanupError));
            }
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ReleaseAsync(IReadOnlyList<string> targets)
    {
        await _gate.WaitAsync();
        try { ReleaseCore(targets); } finally { _gate.Release(); }
    }

    public async Task ReleaseAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            foreach (var entry in _entries.Values) { entry.ReferenceCount = 1; }
            ReleaseCore(_entries.Keys.ToList());
        }
        finally { _gate.Release(); }
    }

    private void ReleaseCore(IEnumerable<string> targets)
    {
        var errors = new List<Exception>();
        foreach (var target in targets)
        {
            if (!_entries.TryGetValue(target, out var entry)) { continue; }
            if (entry.ReferenceCount > 1)
            {
                entry.ReferenceCount--;
                continue;
            }
            try
            {
                if (entry.Original is null)
                {
                    if (!CredDelete($"TERMSRV/{target}", 1, 0))
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error != NotFound) { throw new Win32Exception(error, "Geçici RDP kimlik bilgisi silinemedi."); }
                    }
                }
                else
                {
                    Write(target, entry.Original);
                }
                entry.Original?.Clear();
                _entries.Remove(target);
            }
            catch (Win32Exception ex)
            {
                Trace.TraceWarning($"RDP credential restore failed: {ex.NativeErrorCode}");
                errors.Add(ex);
            }
        }
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Bazı RDP kimlik bilgileri geri yüklenemedi. İstemci kapanırken tekrar denenecek.", new AggregateException(errors));
        }
    }

    private static Snapshot? Read(string target)
    {
        if (!CredRead($"TERMSRV/{target}", 1, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NotFound) { return null; }
            throw new Win32Exception(error, "Mevcut RDP kimlik bilgisi okunamadı.");
        }
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            var blob = new byte[checked((int)credential.BlobSize)];
            if (blob.Length > 0) { Marshal.Copy(credential.Blob, blob, 0, blob.Length); }
            var attributes = new List<AttributeValue>();
            for (var index = 0; index < credential.AttributeCount; index++)
            {
                var attribute = Marshal.PtrToStructure<NativeAttribute>(
                    IntPtr.Add(credential.Attributes, index * Marshal.SizeOf<NativeAttribute>()));
                var value = new byte[checked((int)attribute.ValueSize)];
                if (value.Length > 0) { Marshal.Copy(attribute.Value, value, 0, value.Length); }
                attributes.Add(new AttributeValue(attribute.Keyword ?? string.Empty, attribute.Flags, value));
            }
            return new Snapshot(credential.Username ?? string.Empty, blob, credential.Persist, credential.Flags,
                credential.Comment, credential.TargetAlias, attributes);
        }
        finally { CredFree(pointer); }
    }

    private static void Write(string target, Snapshot snapshot)
    {
        var blob = IntPtr.Zero;
        var attributes = IntPtr.Zero;
        var valuePointers = new List<(IntPtr Pointer, int Length)>();
        var marshaledAttributes = 0;
        try
        {
            blob = Marshal.AllocCoTaskMem(snapshot.Blob.Length);
            if (snapshot.Blob.Length > 0) { Marshal.Copy(snapshot.Blob, 0, blob, snapshot.Blob.Length); }
            if (snapshot.Attributes.Count > 0)
            {
                attributes = Marshal.AllocCoTaskMem(snapshot.Attributes.Count * Marshal.SizeOf<NativeAttribute>());
                foreach (var attribute in snapshot.Attributes)
                {
                    var value = Marshal.AllocCoTaskMem(attribute.Value.Length);
                    valuePointers.Add((value, attribute.Value.Length));
                    if (attribute.Value.Length > 0) { Marshal.Copy(attribute.Value, 0, value, attribute.Value.Length); }
                    Marshal.StructureToPtr(new NativeAttribute
                    {
                        Keyword = attribute.Keyword,
                        Flags = attribute.Flags,
                        ValueSize = (uint)attribute.Value.Length,
                        Value = value
                    }, IntPtr.Add(attributes, marshaledAttributes * Marshal.SizeOf<NativeAttribute>()), false);
                    marshaledAttributes++;
                }
            }
            var credential = new NativeCredential
            {
                Type = 1,
                TargetName = $"TERMSRV/{target}",
                Flags = snapshot.Flags,
                Username = snapshot.Username,
                BlobSize = (uint)snapshot.Blob.Length,
                Blob = blob,
                Persist = snapshot.Persist,
                Comment = snapshot.Comment,
                TargetAlias = snapshot.TargetAlias,
                AttributeCount = (uint)snapshot.Attributes.Count,
                Attributes = attributes
            };
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "RDP kimlik bilgisi kaydedilemedi.");
            }
        }
        finally
        {
            ClearUnmanaged(blob, snapshot.Blob.Length);
            for (var index = 0; index < marshaledAttributes; index++)
            {
                Marshal.DestroyStructure<NativeAttribute>(IntPtr.Add(attributes, index * Marshal.SizeOf<NativeAttribute>()));
            }
            foreach (var value in valuePointers) { ClearUnmanaged(value.Pointer, value.Length); }
            if (attributes != IntPtr.Zero) { Marshal.FreeCoTaskMem(attributes); }
        }
    }

    private static void ClearUnmanaged(IntPtr pointer, int length)
    {
        if (pointer == IntPtr.Zero) { return; }
        for (var index = 0; index < length; index++) { Marshal.WriteByte(pointer, index, 0); }
        Marshal.FreeCoTaskMem(pointer);
    }

    private sealed class Entry(Snapshot? original)
    {
        public Snapshot? Original { get; } = original;
        public int ReferenceCount { get; set; }
    }

    private sealed record AttributeValue(string Keyword, uint Flags, byte[] Value);
    private sealed record Snapshot(string Username, byte[] Blob, uint Persist, uint Flags,
        string? Comment, string? TargetAlias, List<AttributeValue> Attributes)
    {
        public void Clear()
        {
            Array.Clear(Blob);
            foreach (var attribute in Attributes) { Array.Clear(attribute.Value); }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags, Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Username;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeAttribute
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? Keyword;
        public uint Flags, ValueSize;
        public IntPtr Value;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr pointer);
}
