namespace GA.Core.Tests;

using Core;

public class NullableExtensionsTests
{
    [Test]
    public void TryGetValue_ReturnsFalse_WhenNull()
    {
        int? value = null;

        var success = value.TryGetValue(out var v);

        Assert.That(success, Is.False);
        Assert.That(v, Is.EqualTo(0));
    }

    [Test]
    public void TryGetValue_ReturnsTrue_AndOutputsValue_WhenHasValue()
    {
        int? value = 42;

        var success = value.TryGetValue(out var v);

        Assert.That(success, Is.True);
        Assert.That(v, Is.EqualTo(42));
    }
}
