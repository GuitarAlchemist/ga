namespace GA.Core.Tests.Collections;

using Core.Collections;

public class ValueListTests
{
    [Test]
    public void Default_Count_IsZero()
    {
        var list = default(ValueList<int>);

        Assert.That(list.Count, Is.EqualTo(0));
    }

    [Test]
    public void Default_Enumeration_IsEmpty()
    {
        var list = default(ValueList<int>);

        Assert.That(list, Is.Empty);
    }

    [Test]
    public void Default_Equals_Default_IsTrue()
    {
        var a = default(ValueList<int>);
        var b = default(ValueList<int>);

        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void Default_GetHashCode_IsZero()
    {
        var list = default(ValueList<int>);

        Assert.That(list.GetHashCode(), Is.EqualTo(0));
    }

    [Test]
    public void Default_ToString_IsEmptyBrackets()
    {
        var list = default(ValueList<int>);

        Assert.That(list.ToString(), Is.EqualTo("[]"));
    }

    [Test]
    public void Default_Indexer_ThrowsArgumentOutOfRangeException_NotNullReferenceException()
    {
        var list = default(ValueList<int>);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = list[0]);
    }
}
