namespace GA.Business.ML.Agents.Memory;

using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared atomic-write helper for the JSON-backed memory and transcript
/// stores. Ensures every persisted file lands with owner-only permissions
/// where the platform supports them, then atomic-renames into place so a
/// crash mid-write can't leave a half-flushed file that subsequent
/// <c>Load()</c> calls would silently swallow.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists (PR #174 follow-up, expanded 2026-05-11):</b> both
/// <see cref="MemoryStore"/> and <see cref="ChatTranscriptStore"/> persist
/// per-session user data (preferences, focus statements, full chat
/// transcript text). Default <see cref="File.WriteAllText(string, string)"/>
/// on Unix uses the process umask — typically <c>0644</c> — so other users
/// on a shared host can read the file. On Windows the user-profile
/// directory (<c>%USERPROFILE%\.ga</c>) is restricted by inherited ACLs in
/// practice, but a fresh file at a non-profile path (e.g. a custom
/// <c>StorageDirectory</c> configured against a shared scratch dir) would
/// inherit the parent's ACL — which on shared CI agents and bastions can
/// include Authenticated Users read.
/// </para>
/// <para>
/// <b>What this guarantees:</b>
/// <list type="bullet">
///   <item>On Unix the persisted file has
///     <c>UnixFileMode.UserRead | UnixFileMode.UserWrite</c> (mode <c>0600</c>)
///     from the moment it appears at its final path — the mode is set on
///     the <c>.tmp</c> file BEFORE the rename, so there's no window where
///     the file exists at the canonical path with a broader mode.</item>
///   <item>On Windows the persisted file has an explicit DACL with
///     inheritance disabled. Allowed principals: the current user
///     (full control), <c>NT AUTHORITY\SYSTEM</c> (full control), and
///     <c>BUILTIN\Administrators</c> (full control). Every other principal
///     — including <c>Authenticated Users</c> and <c>Everyone</c> — is
///     implicitly denied. Applied to the <c>.tmp</c> before the move so
///     the canonical path is never observable with the inherited ACL.</item>
/// </list>
/// </para>
/// <para>
/// <b>Failure mode:</b> on Unix
/// <see cref="File.SetUnixFileMode(string, UnixFileMode)"/> can throw on
/// filesystems that don't support POSIX permissions (rare on the user
/// profile dir; happens on some FAT-mounted shares). On Windows
/// <see cref="FileSystemAclExtensions.SetAccessControl(FileInfo, FileSecurity)"/>
/// can throw on non-NTFS filesystems (e.g. FAT, exFAT, some network
/// mounts) and inside containers with a stripped <c>WindowsIdentity</c>.
/// In both cases the helper catches and logs at Warning level — the
/// write still completes, but the file lands with the platform default
/// ACL/mode. Treat as a defense-in-depth gap visible in logs, not a hard
/// failure.
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
    /// atomically with owner-only permissions on Unix and an explicit
    /// owner-only DACL on Windows. Creates the parent directory if it
    /// doesn't exist.
    /// </summary>
    /// <param name="path">Destination path. The temp file used in transit
    /// is <c>path + ".tmp"</c>.</param>
    /// <param name="content">UTF-8 text content.</param>
    /// <param name="logger">Optional logger for ACL-application failure.
    /// When null, the failure is swallowed silently — acceptable because
    /// the file still lands at <paramref name="path"/>; only the ACL
    /// hardening is missed.</param>
    public static void WriteAtomic(string path, string content, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);

        // Apply owner-only ACL/mode to the tmp file BEFORE the rename so
        // the destination atomically inherits the restricted permissions.
        // Without this ordering, a parallel reader could race in the
        // window between rename and the permission tightening and see the
        // file at its permissive default. Each helper short-circuits on
        // the wrong OS so the analyzer (CA1416) can see the gate.
        TrySetUnixOwnerOnlyMode(tmp, logger);
        TrySetWindowsOwnerOnlyAcl(tmp, logger);

        File.Move(tmp, path, overwrite: true);
    }

    private static void TrySetUnixOwnerOnlyMode(string path, ILogger? logger)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
            return;

        try
        {
            File.SetUnixFileMode(path, OwnerOnlyMode);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "MemoryFileWriter: failed to set 0600 on {Path} — file system " +
                "may not support POSIX permissions (FAT, some network mounts). " +
                "Write proceeds with default mode; defense-in-depth gap visible " +
                "in this log.", path);
        }
    }

    private static void TrySetWindowsOwnerOnlyAcl(string path, ILogger? logger)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            var fileInfo = new FileInfo(path);
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
            // service worker) can't read the memory store.
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            // BUILTIN\Administrators: full control. Admin tooling and
            // ga-cli operations running elevated need this; without it,
            // an admin debugging a customer's machine can't touch the
            // file without taking ownership first.
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            fileInfo.SetAccessControl(security);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "MemoryFileWriter: failed to apply owner-only DACL on Windows " +
                "path {Path}. File falls back to the inherited ACL of its parent " +
                "directory — defense-in-depth gap visible in this log. Most " +
                "common cause: non-NTFS filesystem (FAT, exFAT) or a stripped " +
                "WindowsIdentity inside a container.", path);
        }
    }
}
