using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace TicketingCRM.Core.Security;

/// <summary>
/// Owner-only local storage. Windows uses protected ACLs created with the file;
/// Unix uses 0700 directories and 0600 files. Neither protects against a compromised owner or host administrator.
/// </summary>
public static class PrivateFileSystem
{
    private const UnixFileMode FilePermissions = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode DirectoryPermissions = FilePermissions | UnixFileMode.UserExecute;

    public static void PrepareDirectory(string filePath)
    {
        CheckAncestors(filePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath))!;
        if (OperatingSystem.IsWindows())
        {
            CreateWindowsDirectory(new DirectoryInfo(directory));
            CheckWindows(new DirectoryInfo(directory).GetAccessControl());
        }
        else
        {
            Directory.CreateDirectory(directory, DirectoryPermissions);
            if (File.GetUnixFileMode(directory) != DirectoryPermissions)
                throw new InvalidOperationException("Private directories require owner-only permissions (0700).");
        }
        CheckAncestors(filePath);
    }

    public static void CheckFile(string path)
    {
        CheckAncestors(path);
        if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new InvalidOperationException("Private files must be regular files without symbolic links or reparse points.");
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (OperatingSystem.IsWindows())
        {
            CheckWindows(directory.GetAccessControl());
            CheckWindows(new FileInfo(path).GetAccessControl(), requireProtectedAcl: false);
        }
        else if (File.GetUnixFileMode(path) != FilePermissions || File.GetUnixFileMode(directory.FullName) != DirectoryPermissions)
            throw new InvalidOperationException("Private files and directories require owner-only permissions (0600/0700).");
    }

    public static FileStream Open(string path, FileMode mode)
    {
        if (mode is not (FileMode.CreateNew or FileMode.OpenOrCreate))
            throw new ArgumentException("Private storage supports exclusive creation or a validated existing lock file.", nameof(mode));
        PrepareDirectory(path);
        if (File.Exists(path)) CheckFile(path);
        if (OperatingSystem.IsWindows())
        {
            var security = new FileSecurity();
            var owner = CurrentOwner();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
            var stream = new FileInfo(path).Create(mode, FileSystemRights.FullControl, FileShare.None, 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough, security);
            try { CheckWindows(stream.GetAccessControl(), requireProtectedAcl: false); return stream; }
            catch { stream.Dispose(); throw; }
        }
        return new FileStream(path, new FileStreamOptions
        {
            Mode = mode,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            UnixCreateMode = FilePermissions
        });
    }

    private static void CheckAncestors(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Private storage requires an absolute path.");
        // An alternate data stream is not a separate protected file on Windows.
        if (OperatingSystem.IsWindows() && Path.GetFullPath(path)[Path.GetPathRoot(path)!.Length..].Contains(':'))
            throw new InvalidOperationException("Private storage cannot use alternate data streams.");
        for (var directory = new DirectoryInfo(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Private storage cannot traverse symbolic links or reparse points.");
        if (Path.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Private storage cannot use symbolic links or reparse points.");
    }

    [SupportedOSPlatform("windows")]
    private static SecurityIdentifier CurrentOwner()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User ?? throw new InvalidOperationException("Private storage requires a Windows account identity.");
    }

    [SupportedOSPlatform("windows")]
    private static void CreateWindowsDirectory(DirectoryInfo directory)
    {
        if (directory.Exists) return;
        if (directory.Parent is { Exists: false } parent) CreateWindowsDirectory(parent);
        var owner = CurrentOwner();
        var security = new DirectorySecurity();
        security.SetOwner(owner);
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.Create(security);
    }

    [SupportedOSPlatform("windows")]
    private static void CheckWindows(FileSystemSecurity security, bool requireProtectedAcl = true)
    {
        var owner = CurrentOwner();
        if (!owner.Equals(security.GetOwner(typeof(SecurityIdentifier))) || (requireProtectedAcl && !security.AreAccessRulesProtected))
            throw new InvalidOperationException("Private Windows storage requires the running account as owner and a protected ACL.");
        var ownerHasControl = false;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow) continue;
            if (!owner.Equals(rule.IdentityReference))
                throw new InvalidOperationException("Private Windows storage must grant access only to the running account.");
            if (rule.PropagationFlags == PropagationFlags.None &&
                (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl)
                ownerHasControl = true;
        }
        if (!ownerHasControl) throw new InvalidOperationException("The private storage owner requires full control.");
    }
}
