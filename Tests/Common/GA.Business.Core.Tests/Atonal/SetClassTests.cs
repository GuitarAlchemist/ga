namespace GA.Business.Core.Tests.Atonal;

using GA.Business.Core.Atonal;

internal class SetClassTests
{
    [Test(TestOf = typeof(PitchClassSet))]
    public void Test_PitchClassSet_All()
    {
        // Arrange
        var items = SetClass.Items;
        
        // Act
        var count = items.Count;
        
        // Assert
        // Assert.That(count, Is.EqualTo(200));
    }
}