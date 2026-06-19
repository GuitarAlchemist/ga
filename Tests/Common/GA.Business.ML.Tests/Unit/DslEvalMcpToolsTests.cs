namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Mcp;

/// <summary>
/// Tests for <see cref="DslEvalMcpTools"/> — the MCP bridge from the chatbot's
/// in-process tool registry to the F# closure registry. Contract:
/// <c>docs/contracts/2026-05-06-ga-dsl-eval-contract.md</c> (v0.1).
/// </summary>
/// <remarks>
/// Tests run against the live <see cref="GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry.Global"/>
/// singleton. The F# DSL's builtin closure modules register on first reference;
/// the constructor of any test method here calls <c>new DslEvalMcpTools()</c>
/// which has no side-effects, so we explicitly touch the closures we depend on
/// to ensure they're registered.
/// </remarks>
[TestFixture]
public class DslEvalMcpToolsTests
{
    private static DslEvalMcpTools MakeTool() => new();

    /// <summary>
    /// Calls the F# DomainClosures module's explicit <c>register()</c>
    /// function. Idempotent — re-running just overwrites entries with the
    /// same name in the underlying ConcurrentDictionary.
    /// </summary>
    private static void EnsureClosuresRegistered() => GA.Business.DSL.Closures.BuiltinClosures.DomainClosures.register();

    [OneTimeSetUp]
    public void OneTimeSetUp() => EnsureClosuresRegistered();

    // ── ListClosures ─────────────────────────────────────────────────────────

    [Test]
    public void ListClosures_ReturnsOnlyDomainCategoryClosures()
    {
        var tool = MakeTool();

        var result = tool.ListClosures();

        Assert.That(result.Closures, Is.Not.Empty,
            "DomainClosures should have registered at least one closure");
        Assert.That(result.Closures.All(c => c.Category == "Domain"), Is.True,
            $"all listed closures must be Domain category; saw {string.Join(",", result.Closures.Select(c => c.Category).Distinct())}");
        Assert.That(result.Closures.Any(c => c.Name == "domain.parseChord"), Is.True,
            "expected domain.parseChord to be in the visible set");
    }

    [Test]
    public void ListClosures_ExcludesAgentAndPipelineClosures()
    {
        // Register the Agent closures module too — if any of its closures
        // leaked into the visible set, the assertion below would catch it.
        GA.Business.DSL.Closures.BuiltinClosures.AgentClosures.register();

        var tool = MakeTool();
        var result = tool.ListClosures();

        Assert.That(result.Closures.Any(c => c.Category != "Domain"), Is.False,
            "Agent / Pipeline / Io closures must not appear in the visible set");
    }

    /// <summary>
    /// Pinning regression for PR #151 review (security finding sec-1):
    /// tab.fetch and tab.fetchUrl make outbound HTTP and were previously
    /// mis-categorized as Domain. With ChatbotHub anonymous, that opened
    /// SSRF via prompt injection (cloud metadata, internal services,
    /// localhost ports). Re-categorizing to Io makes them invisible to
    /// ga_dsl_eval. This test fails loudly if anyone reverts the category
    /// or registers a new outbound-HTTP closure under Domain.
    /// </summary>
    [Test]
    public void ListClosures_NetworkClosuresAreNotVisible()
    {
        // Force the Tab closures module to register so any of its outbound
        // closures leaking into Domain would surface here.
        GA.Business.DSL.Closures.BuiltinClosures.TabClosures.register();

        var tool = MakeTool();
        var visible = tool.ListClosures().Closures.Select(c => c.Name).ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(visible, Does.Not.Contain("tab.fetch"),
                "tab.fetch makes outbound HTTP and must not be reachable via ga_dsl_eval");
            Assert.That(visible, Does.Not.Contain("tab.fetchUrl"),
                "tab.fetchUrl is the SSRF primitive — keeping it out of Domain is the only thing between an anonymous chatbot prompt and arbitrary internal-network reads");
        });
    }

    [Test]
    public void EvalClosure_NetworkClosure_ReturnsClosureNotExposed()
    {
        // Even if someone bypasses the visibility filter (e.g. typed the
        // closure name directly), EvalClosure must refuse non-Domain
        // closures with the closure-not-exposed error code per the contract.
        GA.Business.DSL.Closures.BuiltinClosures.TabClosures.register();

        var tool = MakeTool();
        var result = tool.EvalClosure("tab.fetchUrl", new Dictionary<string, string>
        {
            ["url"] = "http://169.254.169.254/latest/meta-data/"
        });

        Assert.That(result.Error, Is.Not.Null,
            "tab.fetchUrl must produce an error envelope, never execute");
        Assert.That(result.Error!.Code,
            Is.AnyOf("closure-not-found", "closure-not-exposed"),
            $"expected gating error; got '{result.Error.Code}': {result.Error.Message}");
    }

    // ── GetClosureSchema ─────────────────────────────────────────────────────

    [Test]
    public void GetClosureSchema_ReturnsSchemaForKnownDomainClosure()
    {
        var tool = MakeTool();

        var result = tool.GetClosureSchema("domain.parseChord");

        Assert.That(result.Error, Is.Null);
        Assert.That(result.Name, Is.EqualTo("domain.parseChord"));
        Assert.That(result.Category, Is.EqualTo("Domain"));
        Assert.That(result.InputSchema.Keys, Has.Member("symbol"));
        Assert.That(result.OutputType, Is.Not.Empty);
    }

    [Test]
    public void GetClosureSchema_IsCaseInsensitive()
    {
        var tool = MakeTool();

        var lower = tool.GetClosureSchema("domain.parseChord");
        var upper = tool.GetClosureSchema("DOMAIN.PARSECHORD");

        Assert.That(lower.Error, Is.Null);
        Assert.That(upper.Error, Is.Null);
        Assert.That(upper.Name, Is.EqualTo(lower.Name));
    }

    [Test]
    public void GetClosureSchema_UnknownClosure_ReturnsClosureNotFound()
    {
        var tool = MakeTool();

        var result = tool.GetClosureSchema("does.not.exist.2026-05-06");

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error, Does.Contain("closure-not-found"));
    }

    [Test]
    public void GetClosureSchema_OverlongName_ReturnsClosureNotFound()
    {
        var tool = MakeTool();

        // 65 chars — over the MaxClosureNameLength cap.
        var overlong = new string('x', 65);
        var result = tool.GetClosureSchema(overlong);

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error, Does.Contain("closure-not-found"));
    }

    // ── EvalClosure ──────────────────────────────────────────────────────────

    [Test]
    public void EvalClosure_HappyPath_ReturnsParsedChordOutput()
    {
        var tool = MakeTool();

        var result = tool.EvalClosure("domain.parseChord", new Dictionary<string, string>
        {
            ["symbol"] = "Cmaj7",
        });

        Assert.That(result.Error, Is.Null,
            $"expected success; got error: {result.Error?.Message}");
        Assert.That(result.ClosureName, Is.EqualTo("domain.parseChord"));
        Assert.That(result.ClosureCategory, Is.EqualTo("Domain"));
        Assert.That(result.ResultJson, Is.Not.Null.And.Not.Empty);
        Assert.That(result.ElapsedMs, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void EvalClosure_TransposeWithIntCoercion_Succeeds()
    {
        var tool = MakeTool();

        // domain.transposeChord declares semitones: int. The arg value comes
        // in as a string and must coerce server-side.
        var result = tool.EvalClosure("domain.transposeChord", new Dictionary<string, string>
        {
            ["symbol"] = "C",
            ["semitones"] = "3",
        });

        Assert.That(result.Error, Is.Null,
            $"int coercion should succeed; got: {result.Error?.Message}");
        Assert.That(result.Result, Is.Not.Null.Or.Property(nameof(DslEvalResult.ResultJson)).Not.Null,
            "transpose returns a chord-symbol string");
    }

    [Test]
    public void EvalClosure_BadIntCoercion_ReturnsArgCoerceFailed()
    {
        var tool = MakeTool();

        var result = tool.EvalClosure("domain.transposeChord", new Dictionary<string, string>
        {
            ["symbol"] = "C",
            ["semitones"] = "high",   // not parseable as int
        });

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Code, Is.EqualTo("arg-coerce-failed"));
        Assert.That(result.Error.Message, Does.Contain("semitones"));
    }

    [Test]
    public void EvalClosure_MissingRequiredArg_ReturnsMissingRequiredArg()
    {
        var tool = MakeTool();

        var result = tool.EvalClosure("domain.transposeChord", new Dictionary<string, string>
        {
            ["symbol"] = "C",
            // missing 'semitones'
        });

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Code, Is.EqualTo("missing-required-arg"));
        Assert.That(result.Error.Message, Does.Contain("semitones"));
    }

    [Test]
    public void EvalClosure_UnknownClosure_ReturnsClosureNotFound()
    {
        var tool = MakeTool();

        var result = tool.EvalClosure("domain.nonexistent.2026-05-06", new Dictionary<string, string>());

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Code, Is.EqualTo("closure-not-found"));
    }

    [Test]
    public void EvalClosure_NullArgs_TreatsAsEmpty()
    {
        var tool = MakeTool();

        // domain.parseChord requires 'symbol' — null args dict means missing.
        var result = tool.EvalClosure("domain.parseChord", args: null);

        Assert.That(result.Error, Is.Not.Null);
        Assert.That(result.Error!.Code, Is.EqualTo("missing-required-arg"));
    }

    [Test]
    public void EvalClosure_OutputJsonIsValid()
    {
        var tool = MakeTool();

        var result = tool.EvalClosure("domain.parseChord", new Dictionary<string, string>
        {
            ["symbol"] = "Cm",
        });

        Assert.That(result.Error, Is.Null);
        Assert.That(result.ResultJson, Is.Not.Null);
        // ResultJson must round-trip through System.Text.Json — proving it's
        // valid JSON even if we can't strictly type the payload.
        Assert.DoesNotThrow(() =>
            System.Text.Json.JsonDocument.Parse(result.ResultJson!),
            "ResultJson must be valid JSON");
    }
}
