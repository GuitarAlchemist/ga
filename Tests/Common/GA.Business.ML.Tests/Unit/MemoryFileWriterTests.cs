namespace GA.Business.ML.Tests.Unit;

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using GA.Business.ML.Agents.Memory;

/// <summary>
/// Pins the atomic-write + owner-only-permission contract shipped by
/// <see cref="MemoryFileWriter"/>. Atomic-rename behavior tested on every
/// platform; the mode-setting branch is asserted on Unix-likes; the
/// explicit-DACL branch is asserted on Windows. Each platform-gated test
/// is decorated with <c>[Platform(...)]</c> so the other platform's CI
/// silently skips rather than failing.
/// </summary>
[TestFixture]
public class MemoryFileWriterTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-fwriter-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Test]
    public void WriteAtomic_WritesContent_AtDestinationPath()
    {
        var path = Path.Combine(_tempDir, "memory.json");
        MemoryFileWriter.WriteAtomic(path, """{"a":1}""");

        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.ReadAllText(path), Is.EqualTo("""{"a":1}"""));
    }

    [Test]
    public void WriteAtomic_CreatesParentDirectory_IfMissing()
    {
        // Nested path with non-existent intermediate directory. The helper
        // must create the parent so first-boot doesn't fail.
        var path = Path.Combine(_tempDir, "nested", "deep", "memory.json");
        MemoryFileWriter.WriteAtomic(path, "x");

        Assert.That(File.Exists(path), Is.True);
        Assert.That(File.ReadAllText(path), Is.EqualTo("x"));
    }

    [Test]
    public void WriteAtomic_OverwritesExistingFile()
    {
        var path = Path.Combine(_tempDir, "memory.json");
        File.WriteAllText(path, "original");

        MemoryFileWriter.WriteAtomic(path, "updated");

        Assert.That(File.ReadAllText(path), Is.EqualTo("updated"),
            "Atomic-rename with overwrite:true must replace existing content " +
            "— that's the whole point of the rename pattern.");
    }

    [Test]
    public void WriteAtomic_DoesNotLeaveTmpFile_OnSuccess()
    {
        var path = Path.Combine(_tempDir, "memory.json");
        MemoryFileWriter.WriteAtomic(path, "x");

        var tmp = path + ".tmp";
        Assert.That(File.Exists(tmp), Is.False,
            "Successful WriteAtomic must rename tmp → path; no leftover .tmp.");
    }

    [Test]
    public void WriteAtomic_NullOrEmptyPath_Throws()
    {
        Assert.That(() => MemoryFileWriter.WriteAtomic("",   "x"), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => MemoryFileWriter.WriteAtomic(null!, "x"), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void WriteAtomic_NullContent_Throws()
    {
        var path = Path.Combine(_tempDir, "memory.json");
        Assert.That(() => MemoryFileWriter.WriteAtomic(path, null!),
            Throws.InstanceOf<ArgumentNullException>());
    }

    [Test]
    [Platform("Linux,Unix,MacOSX")]
    public void WriteAtomic_OnUnix_SetsOwnerOnlyMode()
    {
        // On Linux / macOS / FreeBSD the file must land at mode 0600
        // (owner read + write). On Windows this test is skipped — the
        // mode-setting branch is gated by OperatingSystem.IsLinux() and
        // friends; Windows is covered by the DACL test below.
        var path = Path.Combine(_tempDir, "memory.json");
        MemoryFileWriter.WriteAtomic(path, "x");

        var mode = File.GetUnixFileMode(path);
        Assert.That(mode, Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite),
            $"Expected 0600 (UserRead | UserWrite); got {mode}. Group / other " +
            "permissions are a defense-in-depth gap on shared hosts.");
    }

    [Test]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public void WriteAtomic_OnWindows_AppliesExplicitOwnerOnlyDacl()
    {
        // On Windows the file must land with:
        //   • inheritance disabled (AreAccessRulesProtected == true)
        //   • the current user with FullControl
        //   • NT AUTHORITY\SYSTEM with FullControl
        //   • BUILTIN\Administrators with FullControl
        //   • no Authenticated Users / Everyone Allow ACEs (those are
        //     what the inherited ACL on shared CI agents typically
        //     leaks through)
        var path = Path.Combine(_tempDir, "memory.json");
        MemoryFileWriter.WriteAtomic(path, "x");

        var info     = new FileInfo(path);
        var security = info.GetAccessControl();

        Assert.That(security.AreAccessRulesProtected, Is.True,
            "DACL must be marked Protected (inheritance disabled). " +
            "Without this, the parent dir's ACL leaks back in.");

        var rules = security
            .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();

        // Sanity: every Allow rule we set is FullControl. We don't assert
        // negative ACEs because the absence of an Allow already implies
        // the principal has no access — that's the Windows ACL model.
        using var identity = WindowsIdentity.GetCurrent();
        var userSid  = identity.User!;
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var adminSid  = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);

        Assert.That(rules.Any(r =>
                r.IdentityReference.Equals(userSid) &&
                r.FileSystemRights == FileSystemRights.FullControl &&
                r.AccessControlType == AccessControlType.Allow),
            "Current user must have an explicit Allow FullControl ACE.");

        Assert.That(rules.Any(r =>
                r.IdentityReference.Equals(systemSid) &&
                r.FileSystemRights == FileSystemRights.FullControl &&
                r.AccessControlType == AccessControlType.Allow),
            "NT AUTHORITY\\SYSTEM must have an explicit Allow FullControl ACE " +
            "(backup/AV/service workers running as SYSTEM need read access).");

        Assert.That(rules.Any(r =>
                r.IdentityReference.Equals(adminSid) &&
                r.FileSystemRights == FileSystemRights.FullControl &&
                r.AccessControlType == AccessControlType.Allow),
            "BUILTIN\\Administrators must have an explicit Allow FullControl ACE " +
            "(admin tooling must not require take-ownership).");

        // Explicitly assert that Authenticated Users and Everyone are NOT
        // listed. These are the two principals shared CI agents most
        // commonly inherit from the parent dir.
        var authenticatedUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        var everyoneSid           = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

        Assert.That(rules.Any(r =>
                r.IdentityReference.Equals(authenticatedUsersSid) &&
                r.AccessControlType == AccessControlType.Allow),
            Is.False,
            "Authenticated Users must NOT have an Allow ACE — that's the " +
            "leak vector this DACL is closing.");
        Assert.That(rules.Any(r =>
                r.IdentityReference.Equals(everyoneSid) &&
                r.AccessControlType == AccessControlType.Allow),
            Is.False,
            "Everyone must NOT have an Allow ACE.");

        // Strict-equality contract: exactly three explicit Allow rules.
        // Catches drift where a future change adds a domain-group Allow
        // (e.g. "CI-Agents") without auditing the security implication.
        Assert.That(rules.Count, Is.EqualTo(3),
            "DACL must contain exactly 3 explicit Allow rules: current user, " +
            "SYSTEM, Administrators. Any other rule is unaudited drift.");
    }

    [Test]
    [Platform("Win")]
    [SupportedOSPlatform("windows")]
    public void WriteAtomic_OnWindows_OverwritePreservesExplicitDacl()
    {
        // Reliability concern flagged by the review: File.Move(overwrite:true)
        // must not silently leak the destination's prior ACL back in. The
        // second write at the same path must land with the same explicit
        // DACL (inheritance still disabled, same three SIDs) as the first.
        var path = Path.Combine(_tempDir, "memory.json");
        MemoryFileWriter.WriteAtomic(path, "first");
        MemoryFileWriter.WriteAtomic(path, "second");

        Assert.That(File.ReadAllText(path), Is.EqualTo("second"));

        var security = new FileInfo(path).GetAccessControl();
        Assert.That(security.AreAccessRulesProtected, Is.True,
            "After overwrite, the DACL must still be Protected. Without this, " +
            "a regression in the create-with-DACL primitive would silently fall " +
            "back to the inherited ACL on the second write.");

        var explicitRules = security
            .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToList();
        Assert.That(explicitRules.Count, Is.EqualTo(3),
            "Second write must land with the same 3-ACE DACL, not the prior " +
            "destination's ACL leaked through File.Move/Replace.");
    }
}
