namespace GA.Business.ML.Agents.Memory;

using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared atomic-write helper for the JSON-backed memory and transcript
/// stores. Creates every persisted file with owner-only permissions
/// applied at creation time, then atomic-renames into place so a crash
/// mid-write can't leave a half-flushed file that subsequent
/// <c>Load()</c> calls would silently swallow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why "applied at creation time" matters (security review of PR #187,
/// 2026-05-11):</b> the earlier draft of this helper called
/// <see cref="File.WriteAllText(string, string)"/> first and tightened
/// permissions afterwards. That left a TOCTOU window — typically a few
/// milliseconds — where the <c>.tmp</c> existed at the parent
/// directory's inherited ACL with the full plaintext content already on
/// disk. A co-tenant racing an open-for-read against the predictable
/// <c>.tmp</c> path could obtain a handle that survives a subsequent
/// <c>SetAccessControl</c>: Windows DACL changes do not revoke already
/// open handles. The current implementation closes that window by
/// creating the file already-protected — on Windows via
/// <see cref="FileSystemAclExtensions.Create(FileSecurity, string, FileMode, FileSystemRights, FileShare, int, FileOptions)"/>
/// and on Unix via <see cref="FileStreamOptions.UnixCreateMode"/>.
/// </para>
/// <para>
/// <b>What this guarantees:</b>
/// <list type="bullet">
///   <item>On Unix the persisted file has
///     <c>UnixFileMode.UserRead | UnixFileMode.UserWrite</c> (mode <c>0600</c>)
///     from the moment the inode is created — there is no observable
///     state where it exists with default umask permissions.</item>
///   <item>On Windows the persisted file has an explicit DACL applied
///     at <c>CREATE_NEW</c> time with inheritance disabled. Allowed
///     principals: the current user (full control),
///     <c>NT AUTHORITY\SYSTEM</c> (full control), and
///     <c>BUILTIN\Administrators</c> (full control). Every other
///     principal — including <c>Authenticated Users</c> and
///     <c>Everyone</c> — is implicitly denied. There is no observable
///     state where the file exists at the parent's inherited ACL.</item>
/// </list>
/// </para>
/// <para>
/// <b>Failure mode:</b> if the create-with-permissions primitive fails
/// (non-NTFS filesystem on Windows; some network mounts on Unix that
/// don't honor the create-mode parameter), the helper falls back to the
/// older "write then tighten" pattern AND logs a warning. The brief
/// race window is then visible in logs, not silent. The write itself
/// still completes — defense-in-depth gap, not a hard failure.
/// </para>
/// </remarks>
public static class MemoryFileWriter
{
    /// <summary>
    /// Owner-only POSIX mode: read + write for the owner, nothing for
    /// group or other. Equivalent to <c>chmod 0600</c>.
    /// </summary>
    private const UnixFileMode OwnerOnlyMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/>
    /// atomically with owner-only permissions applied at creation time
    /// on every supported platform. Creates the parent directory if it
    /// doesn't exist.
    /// </summary>
    /// <param name="path">Destination path. The temp file used in transit
    /// is <c>path + ".tmp"</c>.</param>
    /// <param name="content">UTF-8 text content.</param>
    /// <param name="logger">Optional logger for the create-with-perms
    /// fallback path. When null, fallback warnings are swallowed
    /// silently — acceptable because the file still lands at
    /// <paramref name="path"/>; only the TOCTOU-closure guarantee is
    /// lost.</param>
    public static void WriteAtomic(string path, string content, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";

        // Stale .tmp from a prior crash would conflict with CreateNew.
        // Remove it best-effort; if removal fails, the create will throw
        // below and the outer try/finally cleans up after.
        if (File.Exists(tmp))
        {
            try { File.Delete(tmp); } catch (IOException) { /* fall through */ }
            catch (UnauthorizedAccessException) { /* fall through */ }
        }

        try
        {
            WriteWithOwnerOnlyPermissions(tmp, content, logger);
            MoveAtomic(tmp, path, logger);
        }
        catch
        {
            // Best-effort cleanup of the orphaned tmp on any failure path
            // (including OOM, thread abort). We do NOT swallow — we
            // re-throw so the caller sees the original failure. Without
            // this, a crash between WriteWithOwnerOnlyPermissions and
            // MoveAtomic would leave a .tmp on disk; the next call would
            // notice it via the File.Exists(tmp) guard above, but a
            // process kill between Delete and CreateNew could observe a
            // gap. Try/finally + delete-on-entry closes both.
            try { if (File.Exists(tmp)) File.Delete(tmp); }
            catch (IOException) { /* best effort */ }
            catch (UnauthorizedAccessException) { /* best effort */ }
            throw;
        }
    }

    private static void MoveAtomic(string tmp, string path, ILogger? logger)
    {
        try
        {
            File.Move(tmp, path, overwrite: true);
        }
        catch (UnauthorizedAccessException ex)
        {
            // File.Move(overwrite:true) needs DELETE on the destination,
            // which means the destination's DACL must grant the current
            // user delete-or-modify. If the previous WriteAtomic was run
            // as a different identity (service-account → per-user install,
            // or a roaming profile opened on a new machine), the current
            // identity is not in the destination's ACE list and the move
            // fails. File.Replace goes through a Win32 ReplaceFile path
            // that preserves the destination's DACL via the security
            // descriptor of the source, which IS owned by the current
            // user — so it succeeds where Move fails.
            if (!OperatingSystem.IsWindows()) throw;

            logger?.LogWarning(ex,
                "MemoryFileWriter: File.Move(overwrite) into {Path} failed with " +
                "UnauthorizedAccessException — destination DACL may exclude the " +
                "current user (cross-identity install upgrade?). Falling back to " +
                "File.Replace.", path);
            File.Replace(tmp, path, destinationBackupFileName: null);
        }
    }

    private static void WriteWithOwnerOnlyPermissions(string tmp, string content, ILogger? logger)
    {
        if (OperatingSystem.IsWindows())
        {
            WriteWithWindowsOwnerOnlyDacl(tmp, content, logger);
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            WriteWithUnixOwnerOnlyMode(tmp, content, logger);
        }
        else
        {
            // Unknown OS — write plainly, no hardening available.
            File.WriteAllText(tmp, content);
        }
    }

    private static void WriteWithUnixOwnerOnlyMode(string path, string content, ILogger? logger)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
            return;

        try
        {
            // UnixCreateMode applies at open(2)/creat(2) time via the mode
            // argument — the inode is born with 0600. No window where the
            // file exists at the umask-default mode.
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                UnixCreateMode = OwnerOnlyMode,
            };
            using var fs = new FileStream(path, options);
            using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content);
        }
        catch (Exception ex) when (
            ex is NotSupportedException or
                  PlatformNotSupportedException or
                  IOException)
        {
            // Some filesystems (FAT, certain network mounts) don't honor
            // UnixCreateMode at create-time. Fall back to write-then-chmod
            // — brief window where the file exists with default mode.
            logger?.LogWarning(ex,
                "MemoryFileWriter: UnixCreateMode unsupported on {Path}; falling back " +
                "to write-then-chmod pattern. Brief window where file exists with " +
                "default mode — defense-in-depth gap visible in this log.", path);
            File.WriteAllText(path, content);
            try { File.SetUnixFileMode(path, OwnerOnlyMode); }
            catch (Exception modeEx)
            {
                logger?.LogWarning(modeEx,
                    "MemoryFileWriter: fallback chmod 0600 also failed on {Path}. " +
                    "File lands with default mode.", path);
            }
        }
    }

    private static void WriteWithWindowsOwnerOnlyDacl(string path, string content, ILogger? logger)
    {
        if (!OperatingSystem.IsWindows()) return;

        FileSecurity? security;
        try
        {
            security = BuildOwnerOnlyFileSecurity();
        }
        catch (Exception ex) when (
            ex is InvalidOperationException or
                  IdentityNotMappedException or
                  PlatformNotSupportedException)
        {
            // BuildOwnerOnlyFileSecurity can throw if WindowsIdentity.GetCurrent()
            // returns a SID with User == null (stripped container identity).
            // Fall back to a plain write — the file lands at parent ACL.
            logger?.LogWarning(ex,
                "MemoryFileWriter: failed to build owner-only DACL for {Path} " +
                "(stripped WindowsIdentity?). Falling back to plain write — " +
                "file lands at parent directory's inherited ACL. " +
                "Defense-in-depth gap visible in this log.", path);
            File.WriteAllText(path, content);
            return;
        }

        try
        {
            // FileSystemAclExtensions.Create applies the DACL atomically at
            // CREATE_NEW time — the file is born with the locked-down ACL.
            // No observable state where the parent's inherited ACL applies.
            using var fs = new FileInfo(path).Create(
                FileMode.CreateNew,
                FileSystemRights.WriteData | FileSystemRights.Synchronize,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.None,
                security);
            using var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content);
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
                  IOException or
                  PlatformNotSupportedException or
                  NotSupportedException or
                  PrivilegeNotHeldException or
                  ArgumentException)
        {
            // Non-NTFS filesystem (FAT, exFAT, some network mounts) doesn't
            // support DACLs. Fall back to plain write + post-write
            // SetAccessControl — closes most of the gap, but leaves the
            // TOCTOU window the security review flagged. Logged so it's
            // visible in production. The catch list is deliberately narrow:
            // OOM, ThreadInterrupted, AccessViolation, StackOverflow MUST
            // propagate — they indicate the runtime is in a bad state and
            // continuing the write would compound the problem.
            logger?.LogWarning(ex,
                "MemoryFileWriter: failed to create {Path} with pre-applied DACL " +
                "(likely non-NTFS filesystem). Falling back to write-then-DACL " +
                "pattern — brief race window where parent ACL applies before the " +
                "DACL is tightened. Defense-in-depth gap visible in this log.", path);
            File.WriteAllText(path, content);
            TrySetWindowsOwnerOnlyAclOnExisting(path, security, logger);
        }
    }

    private static void TrySetWindowsOwnerOnlyAclOnExisting(string path, FileSecurity security, ILogger? logger)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            new FileInfo(path).SetAccessControl(security);
        }
        catch (Exception ex) when (
            ex is UnauthorizedAccessException or
                  IOException or
                  PlatformNotSupportedException or
                  PrivilegeNotHeldException or
                  ArgumentException)
        {
            logger?.LogWarning(ex,
                "MemoryFileWriter: post-write SetAccessControl also failed on {Path}. " +
                "File falls back to the inherited ACL of its parent directory.", path);
        }
    }

    private static FileSecurity BuildOwnerOnlyFileSecurity()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows-only.");

        var security = new FileSecurity();

        // Strip inherited ACEs — we want a known-explicit DACL, not the
        // parent directory's grab-bag. preserveInheritance:false means
        // we don't copy the inherited rules in as explicit before
        // stripping — they're gone.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        using var identity = WindowsIdentity.GetCurrent();
        var userSid = identity.User
            ?? throw new InvalidOperationException(
                "WindowsIdentity.GetCurrent() returned no User SID — " +
                "running in a stripped/container identity?");

        // Current user: full control.
        security.AddAccessRule(new FileSystemAccessRule(
            userSid,
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        // NT AUTHORITY\SYSTEM: full control. Without this, scheduled
        // tasks running as SYSTEM (e.g. backup, antivirus, the GA
        // service worker) can't read the memory store. Excluding SYSTEM
        // would also be security theater — SeTakeOwnershipPrivilege
        // lets SYSTEM read anything regardless.
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        // BUILTIN\Administrators: full control. Admin tooling and ga-cli
        // operations running elevated need this; without it, an admin
        // debugging a customer's machine would need to take ownership
        // first (generating 4670 events for no security gain — admins
        // can always take ownership).
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));

        return security;
    }
}
