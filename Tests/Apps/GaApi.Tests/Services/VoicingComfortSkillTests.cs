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
}
