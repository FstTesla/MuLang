using MuLang.Text;

namespace MuLang.Tests.Text;

public sealed class SourceTextTests
{
    [Test]
    public void TracksUnicodeScalarOffsets()
    {
        var source = SourceText.From("a😀b");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.Length, Is.EqualTo(3));
            Assert.That(source.GetText(new TextSpan(1, 1)), Is.EqualTo("😀"));
            Assert.That(source.GetPosition(2), Is.EqualTo(new TextPosition(2, 1, 3)));
        }
    }

    [Test]
    public void TracksMixedLineEndings()
    {
        var source = SourceText.From("a\r\nb\nc\rd");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(source.LineCount, Is.EqualTo(4));
            Assert.That(source.GetPosition(3), Is.EqualTo(new TextPosition(3, 2, 1)));
            Assert.That(source.GetPosition(5), Is.EqualTo(new TextPosition(5, 3, 1)));
            Assert.That(source.GetPosition(7), Is.EqualTo(new TextPosition(7, 4, 1)));
        }
    }

    [Test]
    public void RejectsInvalidUtf16()
    {
        Assert.That(
            static () => SourceText.From("\ud800"),
            Throws.ArgumentException
        );
    }
}
