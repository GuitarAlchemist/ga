namespace GA.Business.DSL.Tests;

using GA.Business.DSL.SourceGen;

/// <summary>
/// Tests for the EBNF source generator pipeline:
///   EbnfParser → EbnfRule AST → FSharpEmitter → compilable F# source.
/// </summary>
[TestFixture]
public sealed class EbnfSourceGenTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // EbnfParser tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Test]
    public void Parse_SimpleAlt_ReturnsAltWithCorrectBranches()
    {
        var parser = new EbnfParser();
        var rules = parser.Parse("""note = "C" | "D" | "E" ;""");

        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0].Name, Is.EqualTo("note"));
        Assert.That(rules[0].Body, Is.InstanceOf<EbnfAlt>());

        var alt = (EbnfAlt)rules[0].Body;
        Assert.That(alt.Alternatives, Has.Count.EqualTo(3));
        Assert.That(alt.Alternatives[0], Is.EqualTo(new EbnfLiteral("C")));
        Assert.That(alt.Alternatives[1], Is.EqualTo(new EbnfLiteral("D")));
        Assert.That(alt.Alternatives[2], Is.EqualTo(new EbnfLiteral("E")));
    }

    [Test]
    public void Parse_Sequence_ReturnsSeqWithItems()
    {
        var parser = new EbnfParser();
        var rules = parser.Parse("""root = note , accidental ;""");

        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0].Body, Is.InstanceOf<EbnfSeq>());

        var seq = (EbnfSeq)rules[0].Body;
        Assert.That(seq.Items, Has.Count.EqualTo(2));
        Assert.That(seq.Items[0], Is.EqualTo(new EbnfRef("note")));
        Assert.That(seq.Items[1], Is.EqualTo(new EbnfRef("accidental")));
    }

    [Test]
    public void Parse_Optional_ReturnsEbnfOptional()
    {
        var parser = new EbnfParser();
        var rules = parser.Parse("""chord = root , [ quality ] ;""");

        Assert.That(rules, Has.Count.EqualTo(1));
        var seq = (EbnfSeq)rules[0].Body;
        Assert.That(seq.Items[1], Is.InstanceOf<EbnfOptional>());

        var opt = (EbnfOptional)seq.Items[1];
        Assert.That(opt.Inner, Is.EqualTo(new EbnfRef("quality")));
    }

    [Test]
    public void Parse_Repeat_ReturnsEbnfRepeat()
    {
        var parser = new EbnfParser();
        var rules = parser.Parse("""list = item , { item } ;""");

        Assert.That(rules, Has.Count.EqualTo(1));
        var seq = (EbnfSeq)rules[0].Body;
        Assert.That(seq.Items[1], Is.InstanceOf<EbnfRepeat>());

        var rep = (EbnfRepeat)seq.Items[1];
        Assert.That(rep.Inner, Is.EqualTo(new EbnfRef("item")));
    }

    [Test]
    public void Parse_MultipleRules_ReturnsAll()
    {
        var parser = new EbnfParser();
        var rules = parser.Parse("""
            note = "C" | "D" ;
            accidental = "#" | "b" ;
            root = note , [ accidental ] ;
        """);

        Assert.That(rules, Has.Count.EqualTo(3));
        Assert.That(rules[0].Name, Is.EqualTo("note"));
        Assert.That(rules[1].Name, Is.EqualTo("accidental"));
        Assert.That(rules[2].Name, Is.EqualTo("root"));
    }

    [Test]
    public void Parse_CommentsStripped()
    {
        var parser = new EbnfParser();
        var rules = parser.Parse("""
            (* This is a comment *)
            note = "C" | "D" ;
        """);

        Assert.That(rules, Has.Count.EqualTo(1));
        Assert.That(rules[0].Name, Is.EqualTo("note"));
    }

    [Test]
    public void Parse_EmptyInput_ReturnsEmptyList()
    {
        var parser = new EbnfParser();
        var rules = parser.Parse("");
        Assert.That(rules, Is.Empty);
    }

    [Test]
    public void Parse_ExtendedVoicingsGrammar_Returns5Rules()
    {
        var parser = new EbnfParser();
        var source = """
            voicing = voicing_type, { alteration } ;
            voicing_type = "drop2-4" | "drop2" | "drop3" | "spread" | "cluster" ;
            alteration = accidental, interval ;
            accidental = "#" | "b" ;
            interval = "11" | "13" | "5" | "7" | "9" ;
        """;

        var rules = parser.Parse(source);
        Assert.That(rules, Has.Count.EqualTo(5));
        Assert.That(rules[0].Name, Is.EqualTo("voicing"));
        Assert.That(rules[1].Name, Is.EqualTo("voicing_type"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FSharpEmitter tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Test]
    public void Emit_ContainsModuleDeclaration()
    {
        var rules = new EbnfParser().Parse("""note = "C" | "D" ;""");
        var emitter = new FSharpEmitter("TestGrammar");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("module GA.Business.Core.Generated.TestGrammarGrammar"));
    }

    [Test]
    public void Emit_ContainsOpenFParsec()
    {
        var rules = new EbnfParser().Parse("""note = "C" | "D" ;""");
        var emitter = new FSharpEmitter("TestGrammar");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("open FParsec"));
    }

    [Test]
    public void Emit_AltRule_GeneratesDuCases()
    {
        var rules = new EbnfParser().Parse("""note = "C" | "D" | "E" ;""");
        var emitter = new FSharpEmitter("Test");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("type NoteNode ="));
        Assert.That(output, Does.Contain("| C"));
        Assert.That(output, Does.Contain("| D"));
        Assert.That(output, Does.Contain("| E"));
    }

    [Test]
    public void Emit_SeqRule_GeneratesSingleCaseDu()
    {
        var rules = new EbnfParser().Parse("""
            chord = root , quality ;
            root = "C" ;
            quality = "m" ;
        """);
        var emitter = new FSharpEmitter("Test");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("type ChordNode ="));
        Assert.That(output, Does.Contain("| Chord of RootNode * QualityNode"));
    }

    [Test]
    public void Emit_RepeatField_GeneratesListType()
    {
        var rules = new EbnfParser().Parse("""
            voicing = voicing_type , { alteration } ;
            voicing_type = "drop2" ;
            alteration = "x" ;
        """);
        var emitter = new FSharpEmitter("Test");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("VoicingTypeNode * AlterationNode list"));
    }

    [Test]
    public void Emit_OptionalField_GeneratesOptionType()
    {
        var rules = new EbnfParser().Parse("""
            chord = root , [ quality ] ;
            root = "C" ;
            quality = "m" ;
        """);
        var emitter = new FSharpEmitter("Test");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("RootNode * QualityNode option"));
    }

    [Test]
    public void Emit_GeneratesParseFunction()
    {
        var rules = new EbnfParser().Parse("""note = "C" | "D" ;""");
        var emitter = new FSharpEmitter("Test");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("let parse (input: string)"));
        Assert.That(output, Does.Contain("Result.Ok v"));
        Assert.That(output, Does.Contain("Result.Error msg"));
    }

    [Test]
    public void Emit_GeneratesForwardRefs()
    {
        var rules = new EbnfParser().Parse("""note = "C" ;""");
        var emitter = new FSharpEmitter("Test");
        var output = emitter.Emit(rules);

        Assert.That(output, Does.Contain("createParserForwardedToRef<NoteNode, unit>"));
    }

    [Test]
    public void Emit_LiteralWithSharp_SanitizesToValidIdentifier()
    {
        var rules = new EbnfParser().Parse("""accidental = "#" | "b" ;""");
        var emitter = new FSharpEmitter("Test");
        var output = emitter.Emit(rules);

        // '#' should become 'Sharp', not empty
        Assert.That(output, Does.Contain("| Sharp"));
    }

    [Test]
    public void Emit_ExtendedVoicings_ProducesCompilableOutput()
    {
        var source = """
            voicing = voicing_type, { alteration } ;
            voicing_type = "drop2-4" | "drop2" | "drop3" | "spread" | "cluster" ;
            alteration = accidental, interval ;
            accidental = "#" | "b" ;
            interval = "11" | "13" | "5" | "7" | "9" ;
        """;

        var rules = new EbnfParser().Parse(source);
        var emitter = new FSharpEmitter("ExtendedVoicings");
        var output = emitter.Emit(rules);

        // Verify key structural elements that F# compilation requires
        Assert.That(output, Does.Contain("type VoicingNode ="));
        Assert.That(output, Does.Contain("and VoicingTypeNode ="));
        Assert.That(output, Does.Contain("and AlterationNode ="));
        Assert.That(output, Does.Contain("pipe2"));
        Assert.That(output, Does.Contain("choice ["));
        Assert.That(output, Does.Contain("many (alterationRef)"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Generated parser integration tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Test]
    public void GeneratedExtendedVoicingsParser_ParsesDrop2()
    {
        var result = GA.Business.Core.Generated.ExtendedVoicingsGrammar.parse("drop2");
        Assert.That(result.IsOk, Is.True);
    }

    [Test]
    public void GeneratedExtendedVoicingsParser_ParsesDrop2Sharp9()
    {
        var result = GA.Business.Core.Generated.ExtendedVoicingsGrammar.parse("drop2#9");
        Assert.That(result.IsOk, Is.True);
    }

    [Test]
    public void GeneratedExtendedVoicingsParser_ParsesDrop24WithAlterations()
    {
        var result = GA.Business.Core.Generated.ExtendedVoicingsGrammar.parse("drop2-4b5#11");
        Assert.That(result.IsOk, Is.True);
    }

    [Test]
    public void GeneratedExtendedVoicingsParser_RejectsInvalidInput()
    {
        var result = GA.Business.Core.Generated.ExtendedVoicingsGrammar.parse("invalid");
        Assert.That(result.IsError, Is.True);
    }

    [Test]
    public void Parse_AsciiTabGrammar_DoesNotThrow()
    {
        var grammar = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory,
                @"..\..\..\..\..\..\Common\GA.Business.DSL\Grammars\AsciiTab.ebnf"));
        var parser = new EbnfParser();
        var rules = parser.Parse(grammar);
        Assert.That(rules.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Parse_ChordProgressionGrammar_DoesNotThrow()
    {
        var grammar = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory,
                @"..\..\..\..\..\..\Common\GA.Business.DSL\Grammars\ChordProgression.ebnf"));
        var parser = new EbnfParser();
        Assert.DoesNotThrow(() => parser.Parse(grammar));
    }
}
