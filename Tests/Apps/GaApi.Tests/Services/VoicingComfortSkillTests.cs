namespace GaApi.Tests.Services;

using GaApi.Services;
using GaApi.Skills;

[TestFixture]
[Category("Unit")]
public class VoicingComfortSkillTests
{
    private VoicingComfortSkill _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new VoicingComfortSkill(new VoicingComfortService(new VoicingFilterService()));

    [Test]
    public async Task FBarreQuestion_ShowsTheESHapeBarreWrittenFromTheLowEString()
    {
        var response = await _sut.ExecuteAsync("How do I play an F barre chord, which fingers go where?");

        Assert.That(response.Result, Does.Contain("`1-3-3-2-1-1`"),
            $"The E-shape F barre, written low E first, must be among the suggestions:\n{response.Result}");
    }

    [Test]
    public async Task FBarreQuestion_NeverSuggestsTheIssue886Shapes()
    {
        var response = await _sut.ExecuteAsync("How do I play an F barre chord, which fingers go where?");

        Assert.That(response.Result, Does.Not.Contain("17"), response.Result);
        Assert.That(response.Result, Does.Not.Contain("`x-x-5-x-0-5`"), response.Result);
        Assert.That(response.Result, Does.Not.Contain("`x-x-10-10-0-x`"), response.Result);
    }

    // A technique complaint that names no chord used to get three voicings of an assumed C.
    // Decline instead, so the technique agent answers the question actually asked.
    [TestCase("My barre chords buzz and my hand hurts. What am I doing wrong?")]
    [TestCase("My hand hurts after practicing, is that normal?")]
    public async Task PainComplaintWithoutAChord_Declines(string message)
    {
        var response = await _sut.ExecuteAsync(message);

        Assert.That(response.Declined, Is.True, response.Result);
        Assert.That(response.Result, Does.Not.Contain("voicings for **C**"), response.Result);
    }

    [Test]
    public async Task PainComplaintNamingAChord_StillSuggestsVoicings()
    {
        var response = await _sut.ExecuteAsync("My hand hurts when I play the F barre chord");

        Assert.That(response.Declined, Is.False, response.Result);
        Assert.That(response.Result, Does.Contain("**F**"), response.Result);
    }
}
