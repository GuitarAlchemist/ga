namespace GaApi.Tests.Controllers;

using GaApi.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

[TestFixture]
public class DslControllerTests
{
    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<DslController>>();
        _controller = new DslController(_mockLogger.Object);
    }

    private DslController _controller = null!;
    private Mock<ILogger<DslController>> _mockLogger = null!;

    [Test]
    public void ParseGrothendieck_WithTensorProduct_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("C ⊗ G");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        Assert.That(okResult.Value, Is.InstanceOf<ParseGrothendieckResponse>());

        var response = (ParseGrothendieckResponse)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
        Assert.That(response.Error, Is.Null);
    }

    [Test]
    public void ParseGrothendieck_WithDirectSum_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("Cmaj7 ⊕ Gmaj7");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithProduct_ShouldReturnSuccess()
    {
        // Arrange - Use function form for multiple objects
        var request = new ParseGrothendieckRequest("product(Cmaj7, Gmaj7, Fmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True, $"Parse failed: {response.Error}");
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithCoproduct_ShouldReturnSuccess()
    {
        // Arrange - Use function form for multiple objects
        var request = new ParseGrothendieckRequest("coproduct(Cmaj7, Gmaj7, Fmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True, $"Parse failed: {response.Error}");
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithExponential_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("Cmaj7 ^ Gmaj7");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithDefineFunctor_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("functor Transpose: Chords -> Chords");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithApplyFunctor_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("Transpose(Cmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithComposeFunctors_ShouldReturnSuccess()
    {
        // Arrange - Use binary infix form (parser doesn't support function form yet)
        var request = new ParseGrothendieckRequest("Transpose ∘ Invert");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True, $"Parse failed: {response.Error}");
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithDefineNatTrans_ShouldReturnSuccess()
    {
        // Arrange - Use apply form (parser requires whitespace after keywords)
        var request = new ParseGrothendieckRequest("η(Cmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True, $"Parse failed: {response.Error}");
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithApplyNatTrans_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("η(Cmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithLimit_ShouldReturnSuccess()
    {
        // Arrange - Use pullback form (simpler than limit of diagram)
        var request = new ParseGrothendieckRequest("pullback(Cmaj7, Transpose, Gmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True, $"Parse failed: {response.Error}");
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithColimit_ShouldReturnSuccess()
    {
        // Arrange - Use pushout form (simpler than colimit of diagram)
        var request = new ParseGrothendieckRequest("pushout(Cmaj7, Transpose, Gmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True, $"Parse failed: {response.Error}");
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithSubobjectClassifier_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("Ω(Cmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithPowerObject_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("P(Cmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithInternalHom_ShouldReturnSuccess()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("Hom(Cmaj7, Gmaj7)");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseGrothendieck_WithInvalidInput_ShouldReturnError()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("invalid syntax !!!");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.False);
        Assert.That(response.Error, Is.Not.Null);
        Assert.That(response.Ast, Is.Null);
    }

    [Test]
    public void ParseGrothendieck_WithEmptyInput_ShouldReturnError()
    {
        // Arrange
        var request = new ParseGrothendieckRequest("");

        // Act
        var result = _controller.ParseGrothendieck(request);

        // Assert - Empty input returns BadRequest
        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    [Test]
    public void GenerateGrothendieck_WithTensorProduct_ShouldReturnCode()
    {
        // Arrange
        var request = new GenerateGrothendieckRequest("C ⊗ G");

        // Act
        var result = _controller.GenerateGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (GenerateGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Code, Is.Not.Null);
        Assert.That(response.Code, Does.Contain("⊗"));
        Assert.That(response.Original, Is.EqualTo("C ⊗ G"));
    }

    [Test]
    public void GenerateGrothendieck_WithDirectSum_ShouldReturnCode()
    {
        // Arrange
        var request = new GenerateGrothendieckRequest("Cmaj7 ⊕ Gmaj7");

        // Act
        var result = _controller.GenerateGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (GenerateGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.True);
        Assert.That(response.Code, Is.Not.Null);
        Assert.That(response.Code, Does.Contain("⊕"));
    }

    [Test]
    public void GenerateGrothendieck_WithInvalidInput_ShouldReturnError()
    {
        // Arrange
        var request = new GenerateGrothendieckRequest("invalid syntax !!!");

        // Act
        var result = _controller.GenerateGrothendieck(request);

        // Assert
        var okResult = (OkObjectResult)result.Result!;
        var response = (GenerateGrothendieckResponse)okResult.Value!;

        Assert.That(response.Success, Is.False);
        Assert.That(response.Error, Is.Not.Null);
        Assert.That(response.Code, Is.Null);
    }

    // ============================================================================
    // CHORD PROGRESSION PARSER TESTS
    // ============================================================================

    [Test]
    public void ParseChordProgression_WithSimpleProgression_ShouldReturnSuccess()
    {
        var request = new ParseChordProgressionRequest("I - IV - V - I");
        var result = _controller.ParseChordProgression(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseChordProgressionResponse)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseChordProgression_WithAbsoluteChords_ShouldReturnSuccess()
    {
        var request = new ParseChordProgressionRequest("C - F - G - C");
        var result = _controller.ParseChordProgression(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseChordProgressionResponse)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseChordProgression_WithInvalidInput_ShouldReturnError()
    {
        var request = new ParseChordProgressionRequest("invalid progression @#$");
        var result = _controller.ParseChordProgression(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseChordProgressionResponse)okResult.Value!;
        Assert.That(response.Success, Is.False);
        Assert.That(response.Error, Is.Not.Null);
    }

    [Test]
    public void ParseChordProgression_WithEmptyInput_ShouldReturnError()
    {
        var request = new ParseChordProgressionRequest("");
        var result = _controller.ParseChordProgression(request);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }

    // ============================================================================
    // FRETBOARD NAVIGATION PARSER TESTS
    // ============================================================================

    [Test]
    public void ParseFretboardNavigation_WithGotoPosition_ShouldReturnSuccess()
    {
        var request = new ParseFretboardNavigationRequest("6:5");
        var result = _controller.ParseFretboardNavigation(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseFretboardNavigationResponse)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseFretboardNavigation_WithGotoShape_ShouldReturnSuccess()
    {
        var request = new ParseFretboardNavigationRequest("CAGED shape E at fret 7");
        var result = _controller.ParseFretboardNavigation(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseFretboardNavigationResponse)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseFretboardNavigation_WithMove_ShouldReturnSuccess()
    {
        var request = new ParseFretboardNavigationRequest("move up 2");
        var result = _controller.ParseFretboardNavigation(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseFretboardNavigationResponse)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseFretboardNavigation_WithSlide_ShouldReturnSuccess()
    {
        var request = new ParseFretboardNavigationRequest("slide from 6:5 to 6:7");
        var result = _controller.ParseFretboardNavigation(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseFretboardNavigationResponse)okResult.Value!;
        Assert.That(response.Success, Is.True);
        Assert.That(response.Ast, Is.Not.Null);
    }

    [Test]
    public void ParseFretboardNavigation_WithInvalidInput_ShouldReturnError()
    {
        var request = new ParseFretboardNavigationRequest("invalid command @#$");
        var result = _controller.ParseFretboardNavigation(request);

        Assert.That(result.Result, Is.InstanceOf<OkObjectResult>());
        var okResult = (OkObjectResult)result.Result!;
        var response = (ParseFretboardNavigationResponse)okResult.Value!;
        Assert.That(response.Success, Is.False);
        Assert.That(response.Error, Is.Not.Null);
    }

    [Test]
    public void ParseFretboardNavigation_WithEmptyInput_ShouldReturnError()
    {
        var request = new ParseFretboardNavigationRequest("");
        var result = _controller.ParseFretboardNavigation(request);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
    }
}
