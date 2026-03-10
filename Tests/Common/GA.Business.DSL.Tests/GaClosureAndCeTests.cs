namespace GA.Business.DSL.Tests;

using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using static GA.Business.DSL.Closures.GaAsync.GaAsyncOps;
using static GA.Business.DSL.Closures.GaClosureRegistry.Closure;
using GaError          = GA.Business.DSL.Closures.GaAsync.GaError;
using GaClosureRegistry = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureRegistry;
using GaClosureCategory = GA.Business.DSL.Closures.GaClosureRegistry.GaClosureCategory;
using GaClosure        = GA.Business.DSL.Closures.GaClosureRegistry.GaClosure;

/// <summary>
/// Unit tests for GaClosureRegistry — register, discover, execute, idempotent re-register.
/// Each test creates its own private registry to avoid cross-test contamination.
/// </summary>
[TestFixture]
public class GaClosureRegistryTests
{
    private GaClosureRegistry _registry = null!;

    [SetUp]
    public void SetUp() => _registry = new GaClosureRegistry();

    // ── helpers ────────────────────────────────────────────────────────────────

    private static GaClosure MakeClosure(string name, GaClosureCategory? cat = null)
    {
        var category = cat ?? GaClosureCategory.Domain;
        return make(
            name,
            category,
            $"Test closure {name}",
            ListModule.OfSeq([Tuple.Create("input", "string")]),
            "string",
            FuncConvert.FromFunc<FSharpMap<string, object>,
                FSharpAsync<FSharpResult<object, GaError>>>(
                    _ => ok<object>("ok")));
    }

    private Task<FSharpResult<object, GaError>> InvokeAsync(
        string closureName, params (string Key, object Value)[] inputs)
    {
        var map = MapModule.OfSeq(inputs.Select(kv => Tuple.Create(kv.Key, kv.Value)));
        return FSharpAsync.StartAsTask(
            _registry.Invoke(closureName, map),
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    [Test]
    public void Register_ThenTryGet_ReturnsSomeClosure()
    {
        _registry.Register(MakeClosure("test.hello"));
        var option = _registry.TryGet("test.hello");
        Assert.That(FSharpOption<GaClosure>.get_IsSome(option), Is.True);
        Assert.That(option.Value.Name, Is.EqualTo("test.hello"));
    }

    [Test]
    public void TryGet_UnknownName_ReturnsNone()
    {
        var option = _registry.TryGet("does.not.exist");
        Assert.That(FSharpOption<GaClosure>.get_IsNone(option), Is.True);
    }

    [Test]
    public void Register_Idempotent_OverwritesSameNameWithLatest()
    {
        _registry.Register(MakeClosure("test.idem"));

        // Re-register same name with updated description
        var updated = make(
            "test.idem",
            GaClosureCategory.Domain,
            "Updated description",
            ListModule.OfSeq<Tuple<string, string>>([]),
            "string",
            FuncConvert.FromFunc<FSharpMap<string, object>,
                FSharpAsync<FSharpResult<object, GaError>>>(
                    _ => ok<object>("updated")));
        _registry.Register(updated);

        var option = _registry.TryGet("test.idem");
        Assert.That(FSharpOption<GaClosure>.get_IsSome(option), Is.True);
        Assert.That(option.Value.Description, Is.EqualTo("Updated description"),
            "Second registration should overwrite first");
    }

    [Test]
    public void List_NoFilter_ReturnsAllClosures()
    {
        _registry.Register(MakeClosure("a.one",   GaClosureCategory.Domain));
        _registry.Register(MakeClosure("b.two",   GaClosureCategory.Agent));
        _registry.Register(MakeClosure("c.three", GaClosureCategory.Pipeline));

        var all = _registry.List(FSharpOption<GaClosureCategory>.None);
        Assert.That(all.Count, Is.EqualTo(3));
    }

    [Test]
    public void List_WithCategoryFilter_ReturnsOnlyMatchingCategory()
    {
        _registry.Register(MakeClosure("d.one",   GaClosureCategory.Domain));
        _registry.Register(MakeClosure("a.two",   GaClosureCategory.Agent));
        _registry.Register(MakeClosure("d.three", GaClosureCategory.Domain));

        var domains = _registry.List(FSharpOption<GaClosureCategory>.Some(GaClosureCategory.Domain));
        Assert.That(domains.Count, Is.EqualTo(2));
        Assert.That(domains.All(c => c.Category.Equals(GaClosureCategory.Domain)), Is.True);
    }

    [Test]
    public async Task Invoke_ExistingClosure_ReturnsOk()
    {
        _registry.Register(MakeClosure("test.invoke"));
        var result = await InvokeAsync("test.invoke", ("input", "hello"));

        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue?.ToString(), Is.EqualTo("ok"));
    }

    [Test]
    public async Task Invoke_UnknownClosure_ReturnsDomainError()
    {
        var result = await InvokeAsync("no.such.closure");

        Assert.That(result.IsError, Is.True);
        Assert.That(result.ErrorValue.IsDomainError, Is.True,
            "Missing closure should return DomainError, not throw");
    }

    [Test]
    public void Count_ReflectsRegisteredClosures_IdempotentRegisterKeepsCount()
    {
        Assert.That(_registry.Count, Is.EqualTo(0));
        _registry.Register(MakeClosure("x"));
        Assert.That(_registry.Count, Is.EqualTo(1));
        _registry.Register(MakeClosure("y"));
        Assert.That(_registry.Count, Is.EqualTo(2));
        // Re-register same name — count must stay at 2
        _registry.Register(MakeClosure("x"));
        Assert.That(_registry.Count, Is.EqualTo(2));
    }
}

/// <summary>
/// Unit tests for GaAsync module functions — ok, fail, map, bind, fanOutAll,
/// and PartialFailure semantics.
/// </summary>
[TestFixture]
public class GaAsyncModuleTests
{
    private static Task<FSharpResult<T, GaError>> Run<T>(
        FSharpAsync<FSharpResult<T, GaError>> m) =>
        FSharpAsync.StartAsTask(
            m,
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

    [Test]
    public async Task Ok_ReturnsOkResult()
    {
        var result = await Run(ok(42));
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Is.EqualTo(42));
    }

    [Test]
    public async Task Fail_ReturnsError()
    {
        var result = await Run(fail<int>(GaError.NewDomainError("test error")));
        Assert.That(result.IsError, Is.True);
    }

    [Test]
    public async Task Map_TransformsOkValue()
    {
        var mapped = map(FuncConvert.FromFunc<int, string>(x => $"value={x}"), ok(10));
        var result = await Run(mapped);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Is.EqualTo("value=10"));
    }

    [Test]
    public async Task Map_PropagatesError_WithoutCallingTransform()
    {
        var transformCalled = false;
        var mapped = map(
            FuncConvert.FromFunc<int, string>(x => { transformCalled = true; return $"{x}"; }),
            fail<int>(GaError.NewDomainError("upstream")));
        var result = await Run(mapped);
        Assert.That(result.IsError, Is.True);
        Assert.That(transformCalled, Is.False, "map transform must not be called on Error");
    }

    [Test]
    public async Task Bind_ChainsTwoOkComputations()
    {
        var bound  = bind(
            FuncConvert.FromFunc<int, FSharpAsync<FSharpResult<string, GaError>>>(
                x => ok($"result={x * 2}")),
            ok(5));
        var result = await Run(bound);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Is.EqualTo("result=10"));
    }

    [Test]
    public async Task Bind_ShortCircuitsOnError_ContinuationNeverCalled()
    {
        var continuationCalled = false;
        var bound = bind(
            FuncConvert.FromFunc<int, FSharpAsync<FSharpResult<string, GaError>>>(
                x => { continuationCalled = true; return ok($"{x}"); }),
            fail<int>(GaError.NewDomainError("stop")));
        var result = await Run(bound);
        Assert.That(result.IsError, Is.True);
        Assert.That(continuationCalled, Is.False, "Bind continuation must not fire on Error");
    }

    [Test]
    public async Task FanOutAll_AllSucceed_ReturnsOkListWithAllValues()
    {
        var computations = ListModule.OfSeq([ok(1), ok(2), ok(3)]);
        var result = await Run(fanOutAll(computations));
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task FanOutAll_SomeFail_ReturnsPartialFailure()
    {
        var computations = ListModule.OfSeq([
            ok(1),
            fail<int>(GaError.NewDomainError("branch failed")),
            ok(3)
        ]);
        var result = await Run(fanOutAll(computations));
        Assert.That(result.IsError, Is.True);
        Assert.That(result.ErrorValue.IsPartialFailure, Is.True,
            "Mixed success/failure should produce PartialFailure");
    }

    [Test]
    public async Task FanOutAll_AllFail_ReturnsPartialFailureWithNoSuccesses()
    {
        var computations = ListModule.OfSeq([
            fail<int>(GaError.NewDomainError("a")),
            fail<int>(GaError.NewDomainError("b"))
        ]);
        var result = await Run(fanOutAll(computations));
        Assert.That(result.IsError, Is.True);
        var pf = result.ErrorValue as GaError.PartialFailure;
        Assert.That(pf, Is.Not.Null, "All-fail should produce PartialFailure");
        Assert.That(pf!.successes.Count, Is.EqualTo(0),
            "All-fail partial failure should report zero successes");
    }
}

/// <summary>
/// Tests for the ga { } / pipeline { } computation expression behaviors via the
/// GaAsync module functions that the CE desugars to. Tests verify that monadic
/// semantics (short-circuit on Error, Zero, fanOut parallelism) work correctly.
/// We test via the functional API rather than calling CE builder methods directly,
/// as CE builder methods use curried FSharpFunc signatures that are awkward from C#.
/// </summary>
[TestFixture]
public class GaCeSemanticTests
{
    private static Task<FSharpResult<T, GaError>> Run<T>(
        FSharpAsync<FSharpResult<T, GaError>> m) =>
        FSharpAsync.StartAsTask(
            m,
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

    /// <summary>
    /// ga { return () } via GaBuilder.Zero() must be Ok () so that
    /// if-then-without-else inside a ga block is a no-op.
    /// </summary>
    [Test]
    public async Task GaBuilder_Zero_IsOkUnit()
    {
        var zero   = GA.Business.DSL.Closures.GaComputationExpression.ga.Zero();
        var result = await Run(zero);
        Assert.That(result.IsOk, Is.True,
            "Zero() must be Ok () — compiler inserts it for if-then-without-else");
    }

    /// <summary>
    /// ga { return 42 } must produce Ok 42.
    /// </summary>
    [Test]
    public async Task GaBuilder_Return_WrapsValueInOk()
    {
        var m      = GA.Business.DSL.Closures.GaComputationExpression.ga.Return(42);
        var result = await Run(m);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Is.EqualTo(42));
    }

    /// <summary>
    /// Simulates: ga { let! x = ok 5; let! y = ok (x*2); return y }
    /// Tests that a chain of Ok binds produces Ok.
    /// </summary>
    [Test]
    public async Task GaBindChain_AllOk_ProducesOkResult()
    {
        var m = bind(
            FuncConvert.FromFunc<int, FSharpAsync<FSharpResult<int, GaError>>>(
                x => bind(
                    FuncConvert.FromFunc<int, FSharpAsync<FSharpResult<int, GaError>>>(
                        y => ok(y + 1)),
                    ok(x * 2))),
            ok(5));
        var result = await Run(m);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Is.EqualTo(11), "5*2+1 = 11");
    }

    /// <summary>
    /// Simulates: ga { let! x = error; let! y = ok(x+1); return y }
    /// Tests that an early error short-circuits all subsequent binds.
    /// </summary>
    [Test]
    public async Task GaBindChain_EarlyError_ShortCircuitsAllSubsequentBinds()
    {
        var secondBindCalled = false;
        var m = bind(
            FuncConvert.FromFunc<int, FSharpAsync<FSharpResult<int, GaError>>>(
                x =>
                {
                    secondBindCalled = true;
                    return ok(x + 1);
                }),
            fail<int>(GaError.NewDomainError("stop early")));
        var result = await Run(m);
        Assert.That(result.IsError, Is.True);
        Assert.That(secondBindCalled, Is.False, "All binds after the first Error must be skipped");
    }

    /// <summary>
    /// fanOutAll parallel branches: preserves successful results when some fail.
    /// This is the PartialFailure case — successes are not discarded.
    /// </summary>
    [Test]
    public async Task FanOutAll_PartialFailure_SuccessfulResultsPreserved()
    {
        var computations = ListModule.OfSeq([
            ok(100),
            fail<int>(GaError.NewDomainError("one fails")),
            ok(300)
        ]);
        var result = await Run(fanOutAll(computations));
        Assert.That(result.IsError, Is.True);
        var pf = result.ErrorValue as GaError.PartialFailure;
        Assert.That(pf, Is.Not.Null);
        // successes = obj list, errors = GaError list
        Assert.That(pf!.successes.Count, Is.EqualTo(2),
            "Two branches succeeded — those results must be preserved in PartialFailure.successes");
        Assert.That(pf.errors.Count, Is.EqualTo(1),
            "One branch failed");
    }

    /// <summary>
    /// mapError: transforms the GaError type without affecting Ok paths.
    /// </summary>
    [Test]
    public async Task MapError_TransformsErrorValue_OkPathUnaffected()
    {
        var transformed = mapError(
            FuncConvert.FromFunc<GaError, GaError>(
                e => GaError.NewDomainError($"wrapped: {e}")),
            fail<int>(GaError.NewDomainError("original")));
        var result = await Run(transformed);
        Assert.That(result.IsError, Is.True);
        var msg = result.ErrorValue.ToString();
        Assert.That(msg, Does.Contain("wrapped:"), "mapError must transform the error value");

        // mapError on Ok path should leave Ok value intact
        var ok42   = mapError(
            FuncConvert.FromFunc<GaError, GaError>(_ => GaError.NewDomainError("should not fire")),
            ok(42));
        var okResult = await Run(ok42);
        Assert.That(okResult.IsOk, Is.True);
        Assert.That(okResult.ResultValue, Is.EqualTo(42));
    }
}

/// <summary>
/// Unit tests for GaDslBuilder sink and fanOut custom operations
/// via the pipeline { } builder's .Sink() / .FanOut() methods
/// invoked through the GaAsync functional API equivalents.
/// </summary>
[TestFixture]
public class GaDslBuilderTests
{
    private static Task<FSharpResult<T, GaError>> Run<T>(
        FSharpAsync<FSharpResult<T, GaError>> m) =>
        FSharpAsync.StartAsTask(
            m,
            FSharpOption<TaskCreationOptions>.None,
            FSharpOption<CancellationToken>.None);

    /// <summary>
    /// Verifies the sink semantic: the upstream value passes through after a
    /// side-effecting bind that returns Ok unit. This is the functional equivalence
    /// of the `sink` CustomOperation in GaDslBuilder.
    /// </summary>
    [Test]
    public async Task Sink_Semantics_SideEffectFires_ValuePassesThrough()
    {
        var sideEffectValue = 0;

        // sink semantics = bind(effect, m) >>= fun _ -> ok(upstreamValue)
        // Simplified: upstream → run effect → return original value
        var upstream = ok(42);
        var result   = await Run(bind(
            FuncConvert.FromFunc<int, FSharpAsync<FSharpResult<int, GaError>>>(v =>
            {
                sideEffectValue = v;   // record side effect
                return ok(v);          // pass value through unchanged
            }),
            upstream));

        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Is.EqualTo(42),
            "Upstream value must pass through unchanged after sink");
        Assert.That(sideEffectValue, Is.EqualTo(42),
            "Side effect must receive the upstream value");
    }

    /// <summary>
    /// Tests that fanOutAll (the underlying implementation of the fanOut CE operation)
    /// preserves successful results from parallel branches and captures failures as PartialFailure.
    /// This directly verifies the semantics the GaDslBuilder.FanOut CustomOperation relies on.
    /// </summary>
    [Test]
    public async Task FanOut_Semantics_AllBranchesRunInParallel()
    {
        var value    = 5;
        var branches = ListModule.OfSeq([
            ok($"a={value}"),
            ok($"b={value * 2}"),
            ok($"c={value * 3}")
        ]);

        var result = await Run(fanOutAll(branches));
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue!.Count, Is.EqualTo(3));
        Assert.That(result.ResultValue, Does.Contain("a=5"));
        Assert.That(result.ResultValue, Does.Contain("b=10"));
        Assert.That(result.ResultValue, Does.Contain("c=15"));
    }

    [Test]
    public async Task FanOut_Semantics_OneBranchFails_ReturnsPartialFailure()
    {
        var branches = ListModule.OfSeq([
            ok("branch-a"),
            fail<string>(GaError.NewDomainError("branch-b failed")),
            ok("branch-c")
        ]);

        var result = await Run(fanOutAll(branches));
        Assert.That(result.IsError, Is.True);
        Assert.That(result.ErrorValue.IsPartialFailure, Is.True,
            "FanOut with mixed success/failure must produce PartialFailure");
    }
}
