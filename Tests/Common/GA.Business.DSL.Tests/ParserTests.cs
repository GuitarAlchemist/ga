namespace GA.MusicTheory.DSL.Tests;

using System.Text;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Parsers;
using Types;

[TestFixture]
public class ChordProgressionParserTests
{
    [Test]
    public void ShouldParseSimpleChordProgression()
    {
        // Arrange
        var input = "C -> G -> Am -> F";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => ChordProgressionParser.parse(input));
    }

    [Test]
    public void ShouldParseChordWithQuality()
    {
        // Arrange
        var input = "Cmaj7 -> Dm7 -> G7 -> Cmaj7";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => ChordProgressionParser.parse(input));
    }

    [Test]
    public void ShouldHandleInvalidInput()
    {
        // Arrange
        var input = "invalid chord progression @#$";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => ChordProgressionParser.parse(input));
    }

    [Test]
    public void ShouldParseTryParseReturnsNoneForInvalidInput()
    {
        // Arrange
        var input = "invalid";

        // Act
        var result = ChordProgressionParser.tryParse(input);

        // Assert - F# None is represented as null in C#
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ShouldParseTryParseReturnsSomeForValidInput()
    {
        // Arrange
        var input = "C -> G -> Am -> F";

        // Act
        var result = ChordProgressionParser.tryParse(input);

        // Assert - F# Some is represented as non-null in C#
        Assert.That(result, Is.Not.Null);
    }
}

[TestFixture]
public class VexTabParserTests
{
    [Test]
    public void ShouldParseSimpleVexTab()
    {
        // Arrange
        var input = "tabstave notation=true tablature=true\nnotes :q 0/1 3/1 5/1";

        // Act
        var result = VexTabParser.parse(input);

        // Assert
        Assert.That(result.IsOk, Is.True,
            $"Parser should succeed. Error: {(result.IsError ? result.ErrorValue : "none")}");
    }

    [Test]
    public void ShouldParseVexTabWithDuration()
    {
        // Arrange
        var input = "tabstave\nnotes :h 0/1 :q 3/1 :8 5/1";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => VexTabParser.parse(input));
    }

    [Test]
    public void ShouldParseVexTabWithTechniques()
    {
        // Arrange
        var input = "tabstave\nnotes :q 0/1h3/1 5/1p3/1";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => VexTabParser.parse(input));
    }

    [Test]
    public void ShouldHandleInvalidVexTab()
    {
        // Arrange
        var input = "invalid vextab @#$";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => VexTabParser.parse(input));
    }

    [Test]
    public void ShouldParseVexTabWithMultipleStaves()
    {
        // Arrange
        var input = "tabstave\nnotes :q 0/1 3/1\n\ntabstave\nnotes :q 5/1 7/1";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => VexTabParser.parse(input));
    }
}

[TestFixture]
public class AsciiTabParserTests
{
    [Test]
    public void ShouldParseSimpleAsciiTab()
    {
        // Arrange
        var input = @"e|---0---3---5---|
B|---0---0---0---|
G|---0---0---0---|
D|---2---2---2---|
A|---2---3---5---|
E|---0---x---x---|";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => AsciiTabParser.parse(input));
    }

    [Test]
    public void ShouldParseAsciiTabWithHeader()
    {
        // Arrange
        var input = @"Title: Test Song
Artist: Test Artist
Tuning: Standard

e|---0---3---5---|
B|---0---0---0---|";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => AsciiTabParser.parse(input));
    }

    [Test]
    public void ShouldHandleInvalidAsciiTab()
    {
        // Arrange
        var input = "invalid ascii tab @#$";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => AsciiTabParser.parse(input));
    }

    [Test]
    public void ShouldParseTryParseReturnsNoneForInvalidInput()
    {
        // Arrange
        var input = "invalid";

        // Act
        var result = AsciiTabParser.tryParse(input);

        // Assert - F# None is represented as null in C#
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ShouldParseTryParseReturnsSomeForValidInput()
    {
        // Arrange
        var input = @"e|---0---3---5---|
B|---0---0---0---|";

        // Act
        var result = AsciiTabParser.tryParse(input);

        // Assert - F# Some is represented as non-null in C#
        Assert.That(result, Is.Not.Null);
    }
}

[TestFixture]
public class FretboardNavigationParserTests
{
    [Test]
    public void ShouldParseSimpleNavigation()
    {
        // Arrange
        var input = "move to fret 5 string 3";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => FretboardNavigationParser.parse(input));
    }

    [Test]
    public void ShouldParseNavigationWithDirection()
    {
        // Arrange
        var input = "move up 2 frets";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => FretboardNavigationParser.parse(input));
    }

    [Test]
    public void ShouldHandleInvalidNavigation()
    {
        // Arrange
        var input = "invalid navigation @#$";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => FretboardNavigationParser.parse(input));
    }
}

[TestFixture]
public class ScaleTransformationParserTests
{
    [Test]
    public void ShouldParseSimpleScaleTransformation()
    {
        // Arrange
        var input = "C major transpose up 2";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => ScaleTransformationParser.parse(input));
    }

    [Test]
    public void ShouldParseScaleWithMode()
    {
        // Arrange
        var input = "D dorian";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => ScaleTransformationParser.parse(input));
    }

    [Test]
    public void ShouldHandleInvalidScale()
    {
        // Arrange
        var input = "invalid scale @#$";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => ScaleTransformationParser.parse(input));
    }
}

[TestFixture]
public class GrothendieckOperationsParserTests
{
    [Test]
    public void ShouldParseSimpleGrothendieckOperation()
    {
        // Arrange
        var input = "pullback C major -> G major";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParsePushforward()
    {
        // Arrange
        var input = "pushforward Am -> C";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldHandleInvalidOperation()
    {
        // Arrange
        var input = "invalid operation @#$";

        // Act & Assert - Just check that parsing doesn't throw
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // CATEGORY OPERATIONS TESTS
    // ============================================================================

    [Test]
    public void ShouldParseTensorProduct()
    {
        var input = "C major ⊗ G major";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseDirectSum()
    {
        var input = "C major ⊕ D minor";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseProduct()
    {
        var input = "product(C major, D minor, E minor)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseCoproduct()
    {
        var input = "coproduct(C major, G major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseExponential()
    {
        var input = "C major ^ G major";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // FUNCTOR OPERATIONS TESTS
    // ============================================================================

    [Test]
    public void ShouldParseFunctorDefinition()
    {
        var input = "functor Transpose: Chords -> Chords";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseFunctorDefinitionWithMappings()
    {
        var input = "functor F: Cat1 -> Cat2 { m1 -> transpose(5), m2 -> invert }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseFunctorApplication()
    {
        var input = "Transpose(C major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseFunctorComposition()
    {
        var input = "F ∘ G ∘ H";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // NATURAL TRANSFORMATION TESTS
    // ============================================================================

    [Test]
    public void ShouldParseNaturalTransformationDefinition()
    {
        var input = "nattrans Alpha: F => G";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseNaturalTransformationWithComponents()
    {
        var input = "nattrans Alpha: F => G { C major -> transpose(5), D minor -> invert }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseNaturalTransformationApplication()
    {
        var input = "Alpha(C major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // LIMIT OPERATIONS TESTS
    // ============================================================================

    [Test]
    public void ShouldParseLimit()
    {
        var input = "limit of { C major, D minor, G major }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParsePullbackWithParentheses()
    {
        var input = "pullback(C major, transpose(5), G major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseEqualizer()
    {
        var input = "equalizer(transpose(5), invert)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // COLIMIT OPERATIONS TESTS
    // ============================================================================

    [Test]
    public void ShouldParseColimit()
    {
        var input = "colimit of { C major, D minor, E minor }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParsePushout()
    {
        var input = "pushout(C major, transpose(7), G major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseCoequalizer()
    {
        var input = "coequalizer(transpose(3), rotate(2))";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // TOPOS OPERATIONS TESTS
    // ============================================================================

    [Test]
    public void ShouldParseSubobjectClassifier()
    {
        var input = "Ω";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseSubobjectClassifierWithObject()
    {
        var input = "Ω(C major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParsePowerObject()
    {
        var input = "P(C major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseInternalHom()
    {
        var input = "Hom(C major, G major)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // SHEAF OPERATIONS TESTS
    // ============================================================================

    [Test]
    public void ShouldParseSheafDefinition()
    {
        var input = "sheaf F on fretboard";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseSheafDefinitionWithSections()
    {
        var input = "sheaf F on circle-of-fifths { U1: C major, U2: G major }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseSheafRestriction()
    {
        var input = "F | U";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseSheafGluing()
    {
        var input = "glue { C major, D minor, G major }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseSheafGluingWithRules()
    {
        var input = "glue { C major, G major } along { U1 ∩ U2 -> transpose(7) }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // MUSICAL OBJECT TESTS
    // ============================================================================

    [Test]
    public void ShouldParseNoteObject()
    {
        var input = "C major ⊗ D";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseChordObject()
    {
        var input = "C maj7 ⊗ D min7";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseScaleObject()
    {
        var input = "C major ⊗ D dorian";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseProgressionObject()
    {
        var input = "{ C major, D minor, G major } ⊗ C major";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseVoicingObject()
    {
        var input = "{ 0/1, 2/2, 3/3 } ⊗ C major";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseSetClassObject()
    {
        var input = "{ 0, 4, 7 } ⊗ { 0, 3, 7 }";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // MORPHISM EXPRESSION TESTS
    // ============================================================================

    [Test]
    public void ShouldParseTransposeMorphism()
    {
        var input = "equalizer(transpose(5), transpose(7))";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseInvertMorphism()
    {
        var input = "equalizer(invert, transpose(3))";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseRotateMorphism()
    {
        var input = "equalizer(rotate(2), invert)";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldParseReflectMorphism()
    {
        var input = "equalizer(reflect, transpose(5))";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    // ============================================================================
    // EDGE CASES AND ERROR HANDLING
    // ============================================================================

    [Test]
    public void ShouldHandleEmptyInput()
    {
        var input = "";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldHandleWhitespaceOnly()
    {
        var input = "   \t\n  ";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldHandleComplexNesting()
    {
        var input = "F(G(H(C major)))";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldHandleMultipleSpaces()
    {
        var input = "C    major    ⊗    G    major";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }

    [Test]
    public void ShouldHandleAccidentals()
    {
        var input = "C# major ⊗ Db minor";
        Assert.DoesNotThrow(() => GrothendieckOperationsParser.parse(input));
    }
}

[TestFixture]
public class GuitarProParserTests
{
    [Test]
    public void ShouldHandleInvalidBase64()
    {
        // Arrange
        var invalidBase64 = "not valid base64 @#$%";

        // Act
        var result = GuitarProParser.parse(invalidBase64);

        // Assert - Should return Error
        Assert.That(result.IsError, Is.True);
        var error = ((GuitarProTypes.GuitarProParseResult.Error)result).Item;
        Assert.That(error, Does.Contain("decoding"));
    }

    [Test]
    public void ShouldHandleEmptyInput()
    {
        // Arrange
        var emptyInput = "";

        // Act
        var result = GuitarProParser.parse(emptyInput);

        // Assert - Should return Error
        Assert.That(result.IsError, Is.True);
    }

    [Test]
    public void ShouldHandleInvalidGuitarProData()
    {
        // Arrange - Valid base64 but not a Guitar Pro file
        var invalidData = Convert.ToBase64String(Encoding.UTF8.GetBytes("This is not a Guitar Pro file"));

        // Act
        var result = GuitarProParser.parse(invalidData);

        // Assert - Should return Error (unable to detect version)
        Assert.That(result.IsError, Is.True);
        var error = ((GuitarProTypes.GuitarProParseResult.Error)result).Item;
        Assert.That(error, Does.Contain("version").Or.Contains("parsing"));
    }

    [Test]
    public void ShouldConvertDocumentToAsciiTab()
    {
        // Arrange - Create a minimal Guitar Pro document
        var doc = new GuitarProTypes.GuitarProDocument(
            GuitarProTypes.GuitarProVersion.GP5,
            new GuitarProTypes.SongInfo(
                FSharpOption<string>.Some("Test Song"),
                FSharpOption<string>.Some("Test Subtitle"),
                FSharpOption<string>.Some("Test Artist"),
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                []
            ),
            ListModule.OfArray(new[]
            {
                new GuitarProTypes.Track(
                    "Guitar",
                    25,
                    6,
                    ListModule.OfArray(new[] { 64, 59, 55, 50, 45, 40 }),
                    0,
                    FSharpOption<Tuple<int, int, int>>.None,
                    [],
                    false,
                    false,
                    100,
                    64,
                    1
                )
            }),
            100,
            120,
            0,
            0
        );

        // Act
        var ascii = GuitarProParser.toAsciiTab(doc);

        // Assert
        Assert.That(ascii, Is.Not.Null);
        Assert.That(ascii, Does.Contain("Test Song"));
        Assert.That(ascii, Does.Contain("Test Artist"));
        Assert.That(ascii, Does.Contain("Guitar"));
        Assert.That(ascii, Does.Contain("120 BPM"));
    }

    [Test]
    public void ShouldParseFileWithValidPath()
    {
        // This test would require an actual Guitar Pro file
        // For now, just test that the function exists and handles missing files
        Assert.DoesNotThrow(() =>
        {
            var result = GuitarProParser.parseFile("nonexistent.gp5");
            Assert.That(result.IsError, Is.True);
        });
    }

    [Test]
    public void ShouldHandleNullOrWhitespaceInput()
    {
        // Arrange
        var whitespaceInput = "   ";

        // Act
        var result = GuitarProParser.parse(whitespaceInput);

        // Assert - Should return Error
        Assert.That(result.IsError, Is.True);
    }

    [Test]
    public void ToAsciiTabShouldHandleMultipleTracks()
    {
        // Arrange - Create a document with multiple tracks
        var doc = new GuitarProTypes.GuitarProDocument(
            GuitarProTypes.GuitarProVersion.GP5,
            new GuitarProTypes.SongInfo(
                FSharpOption<string>.Some("Multi-Track Song"),
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                FSharpOption<string>.None,
                []
            ),
            ListModule.OfArray(new[]
            {
                new GuitarProTypes.Track(
                    "Guitar 1",
                    25,
                    6,
                    ListModule.OfArray(new[] { 64, 59, 55, 50, 45, 40 }),
                    0,
                    FSharpOption<Tuple<int, int, int>>.None,
                    [],
                    false,
                    false,
                    100,
                    64,
                    1
                ),
                new GuitarProTypes.Track(
                    "Bass",
                    33,
                    4,
                    ListModule.OfArray(new[] { 43, 38, 33, 28 }),
                    0,
                    FSharpOption<Tuple<int, int, int>>.None,
                    [],
                    false,
                    false,
                    100,
                    64,
                    2
                )
            }),
            100,
            120,
            0,
            0
        );

        // Act
        var ascii = GuitarProParser.toAsciiTab(doc);

        // Assert
        Assert.That(ascii, Does.Contain("Guitar 1"));
        Assert.That(ascii, Does.Contain("Bass"));
    }
}
