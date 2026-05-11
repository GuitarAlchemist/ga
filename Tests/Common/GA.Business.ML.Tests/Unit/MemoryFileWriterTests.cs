namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Memory;

/// <summary>
/// Pins the atomic-write + owner-only-mode contract shipped by
/// <see cref="MemoryFileWriter"/>. Atomic-rename behavior tested on every
/// platform; the mode-setting branch is asserted only on Unix-likes
/// (Windows uses inherited user-profile ACL, no explicit DACL set).
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
        // friends; Windows relies on user-profile ACL inheritance.
        var path = Path.Combine(_tempDir, "memory.json");
        MemoryFileWriter.WriteAtomic(path, "x");

        var mode = File.GetUnixFileMode(path);
        Assert.That(mode, Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite),
            $"Expected 0600 (UserRead | UserWrite); got {mode}. Group / other " +
            "permissions are a defense-in-depth gap on shared hosts.");
    }
}
