using System.Security.AccessControl;
using System.Security.Principal;

internal static class PrivateStorage
{
    // A dedicated directory is required: never apply application ACLs to a drive root.
    public static void Protect(string path, SecurityIdentifier serviceIdentity)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        if (full.Length <= Path.GetPathRoot(full)!.Length)
            throw new InvalidOperationException("BeRDP requires a dedicated private storage directory.");
        var directory = Directory.CreateDirectory(full);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Private storage cannot be a reparse point.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[] { new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), serviceIdentity }.Distinct())
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(security);
        foreach (var file in directory.GetFiles())
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Private storage contains a reparse point.");
            var fileSecurity = new FileSecurity();
            fileSecurity.SetAccessRuleProtection(false, false);
            file.SetAccessControl(fileSecurity);
        }
        foreach (var child in directory.GetDirectories()) Protect(child.FullName, serviceIdentity);
    }

    public static SecurityIdentifier BackendIdentity()
    {
        var name = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\BeRdpBackend", "ObjectName", null) as string;
        return string.IsNullOrEmpty(name) || name.Equals("LocalSystem", StringComparison.OrdinalIgnoreCase)
            ? new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null)
            : (SecurityIdentifier)new NTAccount(name).Translate(typeof(SecurityIdentifier));
    }
}
