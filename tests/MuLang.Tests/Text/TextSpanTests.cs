using MuLang.Core.Text;

namespace MuLang.Tests.Text;

public sealed class TextSpanTests
{
    [Test]
    public void FromBoundsCreatesExpectedSpan()
    {
        TextSpan span = TextSpan.FromBounds(3, 8);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(span.Start, Is.EqualTo(3));
            Assert.That(span.Length, Is.EqualTo(5));
            Assert.That(span.End, Is.EqualTo(8));
        }
    }

    [TestCase(3, true)]
    [TestCase(7, true)]
    [TestCase(8, false)]
    public void ContainsUsesExclusiveEnd(int offset, bool expected)
    {
        TextSpan span = new (3, 5);

        Assert.That(span.Contains(offset), Is.EqualTo(expected));
    }
}
