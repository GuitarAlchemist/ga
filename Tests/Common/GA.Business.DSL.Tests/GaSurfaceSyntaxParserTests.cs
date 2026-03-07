namespace GA.Business.DSL.Tests;

using GA.Business.DSL.Parsers;

/// <summary>
///     Tests for the Phase 4 GA surface syntax parser and transpiler.
/// </summary>
[TestFixture]
public class GaSurfaceSyntaxParserTests
{
    // ============================================================================
    // PARSE — meta / policy
    // ============================================================================

    [Test]
    public void ParseScript_MetaBlock_Succeeds()
    {
        var src = """
                  meta {
                    version = "1.0"
                    author  = "GA"
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True, $"Expected Ok but got: {result}");
        var stmts = result.ResultValue.Statements;
        Assert.That(stmts.Length, Is.EqualTo(1));
        Assert.That(stmts.Head.IsMetaDecl, Is.True);
    }

    [Test]
    public void ParseScript_PolicyBlock_Succeeds()
    {
        var src = """
                  policy {
                    retry   = 3
                    timeout = 30
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue.Statements.Head.IsPolicyDecl, Is.True);
    }

    // ============================================================================
    // PARSE — pipeline / node (prop-bag) / edge
    // ============================================================================

    [Test]
    public void ParseScript_Pipeline_WithPropBagNodes_Succeeds()
    {
        var src = """
                  pipeline embed {
                    node "rooms" kind=io { closure = "pipeline.pullBspRooms" output = rooms }
                    node "embed"  kind=pipe { closure = "pipeline.embedOpticK" input = [rooms] output = vecs }
                    edge "rooms" -> "embed"
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True, $"Parse failed: {(result.IsError ? result.ErrorValue : "")}");

        var stmts = result.ResultValue.Statements;
        Assert.That(stmts.Length, Is.EqualTo(1));
        Assert.That(stmts.Head.IsPipelineDecl, Is.True);
    }

    [Test]
    public void ParseScript_NodeKinds_AllAccepted()
    {
        foreach (var kind in new[] { "io", "domain", "pipe", "agent", "work", "reason" })
        {
            var src = $"pipeline p {{ node n kind={kind} {{ closure = \"foo\" }} }}";
            var result = GaSurfaceSyntaxParser.parseScript(src);
            Assert.That(result.IsOk, Is.True, $"kind={kind} failed: {(result.IsError ? result.ErrorValue : "")}");
        }
    }

    [Test]
    public void ParseScript_EdgeDecl_Parsed()
    {
        var src = """
                  pipeline p {
                    node a kind=io { closure = "c1" }
                    node b kind=pipe { closure = "c2" input = [a] }
                    edge a -> b
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True);
    }

    // ============================================================================
    // PARSE — legacy closure-call body
    // ============================================================================

    [Test]
    public void ParseScript_LegacyClosureCallBody_Succeeds()
    {
        var src = "pipeline p { node n work myClosureName(key: val) }";
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True, $"Legacy body failed: {(result.IsError ? result.ErrorValue : "")}");
        Assert.That(result.ResultValue.Statements[0].IsPipelineDecl, Is.True);
    }

    // ============================================================================
    // PARSE — workflow
    // ============================================================================

    [Test]
    public void ParseScript_Workflow_Succeeds()
    {
        var src = """
                  workflow my-flow {
                    node step1 kind=domain { closure = "domain.parseChord" output = chord }
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue.Statements.Head.IsWorkflowDecl, Is.True);
    }

    // ============================================================================
    // PARSE — let / do
    // ============================================================================

    [Test]
    public void ParseScript_LetBind_Succeeds()
    {
        var src = """
                  pipeline p {
                    let x = 42
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True, $"Parse error: {(result.IsError ? result.ErrorValue : "")}");
    }

    [Test]
    public void ParseScript_DoExpr_Succeeds()
    {
        var src = """
                  pipeline p {
                    do printfn "hello"
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True, $"Parse error: {(result.IsError ? result.ErrorValue : "")}");
    }

    // ============================================================================
    // PARSE — line comments
    // ============================================================================

    [Test]
    public void ParseScript_LineComments_Ignored()
    {
        var src = """
                  // top-level comment
                  pipeline embed {
                    -- dash comment
                    node n kind=io { closure = "foo" output = result }
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True, $"Comment parse failed: {(result.IsError ? result.ErrorValue : "")}");
    }

    // ============================================================================
    // PARSE — list values
    // ============================================================================

    [Test]
    public void ParseScript_ListInputValue_ParsedCorrectly()
    {
        var src = """
                  pipeline p {
                    node n kind=pipe { closure = "c" input = [a, b, c] output = out }
                  }
                  """;
        var result = GaSurfaceSyntaxParser.parseScript(src);
        Assert.That(result.IsOk, Is.True);
    }

    // ============================================================================
    // PARSE — error handling
    // ============================================================================

    [Test]
    public void ParseScript_InvalidSyntax_ReturnsError()
    {
        var result = GaSurfaceSyntaxParser.parseScript("@@@invalid@@@");
        Assert.That(result.IsError, Is.True, "Invalid syntax should return Error");
    }

    [Test]
    public void ParseScript_EmptyInput_ReturnsOkEmptyScript()
    {
        var result = GaSurfaceSyntaxParser.parseScript("");
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue.Statements, Is.Empty);
    }

    // ============================================================================
    // TRANSPILE — roundtrip
    // ============================================================================

    [Test]
    public void Transpile_EmptyInput_ProducesEmptyGaBlock()
    {
        var result = GaSurfaceSyntaxParser.transpile("");
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Does.Contain("ga {"));
    }

    [Test]
    public void Transpile_SinglePropBagNode_ProducesInvoke()
    {
        var src = """
                  pipeline embed {
                    node "rooms" kind=io { closure = "pipeline.pullBspRooms" output = rooms }
                  }
                  """;
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True, $"Transpile failed: {(result.IsError ? result.ErrorValue : "")}");
        var fsharp = result.ResultValue;
        Assert.That(fsharp, Does.Contain("ga {"));
        Assert.That(fsharp, Does.Contain("GaClosureRegistry.Global.Invoke"));
        Assert.That(fsharp, Does.Contain("pipeline.pullBspRooms"));
        Assert.That(fsharp, Does.Contain("let! rooms"));
    }

    [Test]
    public void Transpile_InputWiredFromEdge_BindsInputArg()
    {
        var src = """
                  pipeline embed {
                    node "rooms" kind=io   { closure = "pipeline.pullBspRooms"  output = rooms }
                    node "embed"  kind=pipe { closure = "pipeline.embedOpticK"   input = [rooms] output = vecs }
                    edge "rooms" -> "embed"
                  }
                  """;
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True);
        var fsharp = result.ResultValue;
        Assert.That(fsharp, Does.Contain("let! rooms"));
        Assert.That(fsharp, Does.Contain("let! vecs"));
        Assert.That(fsharp, Does.Contain("pipeline.embedOpticK"));
        // input binding should include "input", rooms
        Assert.That(fsharp, Does.Contain("\"input\""));
        Assert.That(fsharp, Does.Contain("rooms"));
    }

    [Test]
    public void Transpile_MetaDecl_EmitsComment()
    {
        var src = "meta { version = \"1.0\" }";
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Does.Contain("// ── meta"));
    }

    [Test]
    public void Transpile_PolicyDecl_EmitsComment()
    {
        var src = "policy { retry = 3 }";
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Does.Contain("// ── policy"));
    }

    [Test]
    public void Transpile_PipelineKeyword_EmittedInOutput()
    {
        var src = "pipeline myPipe { node n kind=io { closure = \"c\" output = x } }";
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Does.Contain("pipeline {"));
    }

    [Test]
    public void Transpile_InvalidSyntax_ReturnsError()
    {
        var result = GaSurfaceSyntaxParser.transpile("@@@");
        Assert.That(result.IsError, Is.True);
    }

    [Test]
    public void Transpile_MultipleInputVars_ProducesArray()
    {
        var src = """
                  pipeline p {
                    node n kind=pipe { closure = "c" input = [a, b] output = out }
                  }
                  """;
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True);
        // Multiple inputs → array form
        Assert.That(result.ResultValue, Does.Contain("[|"));
    }

    [Test]
    public void Transpile_ExtraPropsBecomeArgs()
    {
        var src = """
                  pipeline p {
                    node n kind=pipe { closure = "c" collection = "my-coll" output = out }
                  }
                  """;
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True);
        Assert.That(result.ResultValue, Does.Contain("collection"));
    }

    // ============================================================================
    // TRANSPILE — full plan example
    // ============================================================================

    [Test]
    public void Transpile_PlanDocExample_ProducesValidFSharp()
    {
        // The canonical example from the GA Language Plan / MCP tool description.
        var src = """
                  pipeline embed {
                    node "rooms" kind=io   { closure = "pipeline.pullBspRooms"  output = rooms }
                    node "embed"  kind=pipe { closure = "pipeline.embedOpticK"   input = [rooms]  output = vecs }
                    node "store"  kind=pipe { closure = "pipeline.storeQdrant"   input = [vecs]   output = n }
                    edge "rooms" -> "embed"
                    edge "embed"  -> "store"
                  }
                  """;
        var result = GaSurfaceSyntaxParser.transpile(src);
        Assert.That(result.IsOk, Is.True, $"Full plan example failed: {(result.IsError ? result.ErrorValue : "")}");
        var fsharp = result.ResultValue;
        Assert.That(fsharp, Does.Contain("pipeline.pullBspRooms"));
        Assert.That(fsharp, Does.Contain("pipeline.embedOpticK"));
        Assert.That(fsharp, Does.Contain("pipeline.storeQdrant"));
        Assert.That(fsharp, Does.Contain("let! rooms"));
        Assert.That(fsharp, Does.Contain("let! vecs"));
        Assert.That(fsharp, Does.Contain("let! n"));
    }
}
