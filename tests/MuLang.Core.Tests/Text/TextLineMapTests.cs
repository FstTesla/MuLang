using MuLang.Core.Text;

namespace MuLang.Core.Tests.Text;

public sealed class TextLineMapTests
{
    [Test]
    public void ConvertsOffsetsPositionsAndLocations()
    {
        SourceText source = SourceText.From("a\r\n😀b\n");
        TextLineMap lineMap = source.LineMap;
        TextSpan span = new (3, 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lineMap.Length, Is.EqualTo(6));
            Assert.That(lineMap.LineStarts, Is.EqualTo([ 0, 3, 6 ]));
            Assert.That(
                lineMap.GetPosition(4),
                Is.EqualTo(new TextPosition(4, 2, 2))
            );
            Assert.That(lineMap.GetOffset(2, 2), Is.EqualTo(4));
            Assert.That(
                lineMap.GetLocation(span, "file:///sample.mu"),
                Is.EqualTo(
                    new TextLocation(
                        span,
                        new TextPosition(3, 2, 1),
                        new TextPosition(5, 2, 3),
                        "file:///sample.mu"
                    )
                )
            );
        }
    }

    [Test]
    public void RejectsInvalidBounds()
    {
        TextLineMap lineMap = new (3, [ 0, 2 ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                () => lineMap.GetPosition(4),
                Throws.TypeOf<ArgumentOutOfRangeException>()
            );
            Assert.That(
                () => lineMap.GetOffset(1, 4),
                Throws.TypeOf<ArgumentOutOfRangeException>()
            );
            Assert.That(
                () => lineMap.GetLocation(new TextSpan(2, 2)),
                Throws.TypeOf<ArgumentOutOfRangeException>()
            );
        }
    }
}
