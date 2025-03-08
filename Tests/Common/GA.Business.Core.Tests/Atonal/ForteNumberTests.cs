namespace GA.Business.Core.Tests.Atonal;

using Core.Atonal;

internal class ForteNumberTests
{
    [Test(TestOf = typeof(ForteNumberTests))]
    public void Test_ForteNumber_All()
    {
        // Arrange
        var items = ForteNumber.Items;
        
        // Act
        var s = string.Join(", ", items);
        var count = items.Count;
        
        // Assert
        // Assert.That(count, Is.EqualTo(200));
    }

}