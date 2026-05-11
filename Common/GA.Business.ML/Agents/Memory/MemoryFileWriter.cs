namespace GA.Business.ML.Agents.Memory;

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
/// <b>Why this exists (PR #174 follow-up):</b> both <see cref="MemoryStore"/>
/// and <see cref="ChatTranscriptStore"/> persist per-session user data
/// (preferences, focus statements, full chat transcript text). Default
/// <see cref="File.WriteAllText(string, string)"/> on Unix uses the
/// process umask — typically <c>0644</c> — so other users on a shared
/// host can read the file. On Windows the user-profile directory
/// (<c>%USERPROFILE%\.ga</c>) is already restricted by inherited ACLs in
/// practice, but the file itself was not explicitly locked down.
/// </para>
/// <para>
/// <b>What this guarantees:</b> on Unix the persisted file has
/// <c>UnixFileMode.UserRead | UnixFileMode.UserWrite</c> (mode <c>0600</c>)
/// from the moment it appears at its final path — the mode is set on the
/// <c>.tmp</c> file BEFORE the rename, so there's no window where the
/// file exists at the canonical path with a broader mode. On Windows the
/// helper is a no-op for ACL — the user-profile ACL inheritance is the
/// load-bearing defense and adding an explicit DACL adds a
/// platform-dependent <c>System.IO.FileSystem.AccessControl</c> dependency
/// that isn't worth the extra surface today.
/// </para>
/// <para>
/// <b>Failure mode:</b> <see cref="File.SetUnixFileMode"/> can throw on
/// filesystems that don't support POSIX permissions (rare on the user
/// profile dir; happens on some FAT-mounted shares). The helper catches
/// and logs at Warning level — the write still completes, but the file
/// lands with default mode. Treat as a defense-in-depth gap visible in
/// logs, not a hard failure.
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
    /// atomically with owner-only permissions on Unix. Creates the parent
    /// directory if it doesn't exist.
    /// </summary>
    /// <param name="path">Destination path. The temp file used in transit
    /// is <c>path + ".tmp"</c>.</param>
    /// <param name="content">UTF-8 text content.</param>
    /// <param name="logger">Optional logger for the
    /// <see cref="File.SetUnixFileMode"/> failure case. When null, the
    /// failure is swallowed silently — acceptable because the file still
    /// lands at <paramref name="path"/>; only the ACL hardening is missed.</param>
    public static void WriteAtomic(string path, string content, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);

        // Apply owner-only mode to the tmp file BEFORE the rename so the
        // destination atomically inherits the restricted mode. Without
        // this ordering, a parallel reader could race in the window
        // between rename and SetUnixFileMode and see the file at its
        // permissive default mode.
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ||
            OperatingSystem.IsFreeBSD())
        {
            try
            {
                File.SetUnixFileMode(tmp, OwnerOnlyMode);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "MemoryFileWriter: failed to set 0600 on {Path} — file system " +
                    "may not support POSIX permissions (FAT, some network mounts). " +
                    "Write proceeds with default mode; defense-in-depth gap visible " +
                    "in this log.", tmp);
            }
        }

        File.Move(tmp, path, overwrite: true);
    }
}
