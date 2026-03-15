namespace GA.Business.DSL.Tests;

using System.IO;
using GA.Business.DSL.Closures.BuiltinClosures;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using GaError = GA.Business.DSL.Closures.GaAsync.GaError;

/// <summary>
/// Security tests for io.readFile, io.writeFile, io.httpGet, and io.httpPost.
/// Each test resets IoSecurityConfig before running to avoid cross-test contamination.
/// </summary>
[TestFixture]
public class IoClosuresSecurityTests
{
    private string _tmpDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tmpDir);

        // Reset to safe defaults before each test.
        IoClosures.IoSecurityConfig.AllowedBasePaths = FSharpList<string>.Empty;
        IoClosures.IoSecurityConfig.AllowedDomains   = FSharpList<string>.Empty;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static async Task<FSharpResult<object, GaError>> RunAsync(
        GA.Business.DSL.Closures.GaClosureRegistry.GaClosure closure,
        params (string key, object value)[] inputs)
    {
        var map = MapModule.OfSeq(inputs.Select(kv =>
            new Tuple<string, object>(kv.key, kv.value)));
        return await FSharpAsync.StartAsTask(
            closure.Exec.Invoke(map),
            null, null);
    }

    private static bool IsError(FSharpResult<object, GaError> result) => result.IsError;
    private static bool IsOk(FSharpResult<object, GaError> result) => result.IsOk;

    private static string ErrorMessage(FSharpResult<object, GaError> result)
    {
        var prop = result.GetType().GetProperty("ErrorValue");
        return prop?.GetValue(result)?.ToString() ?? result.ToString()!;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // io.readFile — file path security
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task ReadFile_AllowedPath_ReturnsContents()
    {
        var file = Path.Combine(_tmpDir, "test.txt");
        File.WriteAllText(file, "hello");
        IoClosures.IoSecurityConfig.AllowedBasePaths =
            ListModule.OfSeq(new[] { _tmpDir });

        var result = await RunAsync(IoClosures.readFile, ("path", (object)file));

        Assert.That(IsOk(result), Is.True);
        var okValue = result.GetType().GetProperty("ResultValue")?.GetValue(result);
        Assert.That(okValue, Is.EqualTo("hello"));
    }

    [Test]
    public async Task ReadFile_NoAllowedPaths_Denies()
    {
        // AllowedBasePaths is empty — all file access should be denied.
        var result = await RunAsync(IoClosures.readFile, ("path", (object)"/etc/passwd"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("disabled"));
    }

    [Test]
    public async Task ReadFile_PathOutsideAllowedBase_Denies()
    {
        IoClosures.IoSecurityConfig.AllowedBasePaths =
            ListModule.OfSeq(new[] { _tmpDir });

        var result = await RunAsync(IoClosures.readFile, ("path", (object)"/etc/passwd"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("outside the allowed"));
    }

    [Test]
    public async Task ReadFile_DotDotTraversal_Denies()
    {
        IoClosures.IoSecurityConfig.AllowedBasePaths =
            ListModule.OfSeq(new[] { _tmpDir });

        // Attempt to escape _tmpDir using ../../
        var traversalPath = Path.Combine(_tmpDir, "..", "..", "etc", "passwd");

        var result = await RunAsync(IoClosures.readFile, ("path", (object)traversalPath));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("outside the allowed"));
    }

    [Test]
    public async Task ReadFile_PrefixSpoofing_Denies()
    {
        // "/tmp/allowed-dir-evil" must NOT match allowed base "/tmp/allowed-dir".
        var allowedDir = Path.Combine(Path.GetTempPath(), "allowed-dir");
        var evilDir    = Path.Combine(Path.GetTempPath(), "allowed-dir-evil");
        Directory.CreateDirectory(evilDir);

        try
        {
            IoClosures.IoSecurityConfig.AllowedBasePaths =
                ListModule.OfSeq(new[] { allowedDir });

            var evilFile = Path.Combine(evilDir, "secret.txt");
            File.WriteAllText(evilFile, "secret");

            var result = await RunAsync(IoClosures.readFile, ("path", (object)evilFile));

            Assert.That(IsError(result), Is.True);
            Assert.That(ErrorMessage(result), Does.Contain("outside the allowed"));
        }
        finally
        {
            if (Directory.Exists(evilDir)) Directory.Delete(evilDir, recursive: true);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // io.writeFile — file path security
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task WriteFile_AllowedPath_WritesFile()
    {
        IoClosures.IoSecurityConfig.AllowedBasePaths =
            ListModule.OfSeq(new[] { _tmpDir });

        var file   = Path.Combine(_tmpDir, "out.txt");
        var result = await RunAsync(IoClosures.writeFile,
            ("path", (object)file), ("content", (object)"world"));

        Assert.That(IsOk(result), Is.True);
        Assert.That(File.ReadAllText(file), Is.EqualTo("world"));
    }

    [Test]
    public async Task WriteFile_PathTraversal_Denies()
    {
        IoClosures.IoSecurityConfig.AllowedBasePaths =
            ListModule.OfSeq(new[] { _tmpDir });

        var traversal = Path.Combine(_tmpDir, "..", "evil.txt");
        var result    = await RunAsync(IoClosures.writeFile,
            ("path", (object)traversal), ("content", (object)"bad"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("outside the allowed"));
        // Ensure the file was NOT created.
        Assert.That(File.Exists(Path.Combine(Path.GetTempPath(), "evil.txt")), Is.False);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // io.httpGet / io.httpPost — SSRF security
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public async Task HttpGet_NoAllowedDomains_Denies()
    {
        var result = await RunAsync(IoClosures.httpGet,
            ("url", (object)"http://example.com/data"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("disabled"));
    }

    [Test]
    public async Task HttpGet_DomainNotInAllowlist_Denies()
    {
        IoClosures.IoSecurityConfig.AllowedDomains =
            ListModule.OfSeq(new[] { "allowed.example.com" });

        var result = await RunAsync(IoClosures.httpGet,
            ("url", (object)"http://evil.example.com/data"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("not in AllowedDomains"));
    }

    [Test]
    public async Task HttpGet_LoopbackAddress_Denies()
    {
        // 127.0.0.1 resolves directly; we put "localhost" in allowlist and
        // expect the IP check to reject it.
        IoClosures.IoSecurityConfig.AllowedDomains =
            ListModule.OfSeq(new[] { "localhost" });

        var result = await RunAsync(IoClosures.httpGet,
            ("url", (object)"http://localhost/internal"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("restricted address"));
    }

    [Test]
    public async Task HttpPost_MetadataEndpoint_Denies()
    {
        // 169.254.169.254 is the AWS/GCP/Azure instance metadata endpoint.
        // Even if someone adds it to AllowedDomains it should be blocked by IP.
        IoClosures.IoSecurityConfig.AllowedDomains =
            ListModule.OfSeq(new[] { "169.254.169.254" });

        var result = await RunAsync(IoClosures.httpPost,
            ("url", (object)"http://169.254.169.254/latest/meta-data/"),
            ("body", (object)"{}"));

        Assert.That(IsError(result), Is.True);
        // Either "not in AllowedDomains" (numeric host match) or "restricted address".
        Assert.That(
            ErrorMessage(result),
            Does.Contain("restricted address").Or.Contain("AllowedDomains"));
    }

    [Test]
    public async Task HttpGet_NonHttpScheme_Denies()
    {
        IoClosures.IoSecurityConfig.AllowedDomains =
            ListModule.OfSeq(new[] { "example.com" });

        var result = await RunAsync(IoClosures.httpGet,
            ("url", (object)"file:///etc/passwd"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("scheme"));
    }

    [Test]
    public async Task HttpGet_InvalidUrl_Denies()
    {
        IoClosures.IoSecurityConfig.AllowedDomains =
            ListModule.OfSeq(new[] { "example.com" });

        var result = await RunAsync(IoClosures.httpGet,
            ("url", (object)"not-a-url"));

        Assert.That(IsError(result), Is.True);
        Assert.That(ErrorMessage(result), Does.Contain("Invalid URL"));
    }
}
