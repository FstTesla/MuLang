using MuLang.Core.Runtime;

namespace MuLang.Core.Tests.Runtime;

public sealed class RuntimeErrorTests
{
    [Test]
    public void DistinguishesAbsentAndPresentNullData()
    {
        RuntimeError absent = new (
            "absent",
            "Absent.",
            RuntimeErrorCategory.Application,
            true,
            default,
            [ ],
            null,
            RuntimeErrorData.Absent
        );
        RuntimeError presentNull = new (
            "present",
            "Present.",
            RuntimeErrorCategory.Application,
            true,
            default,
            [ ],
            null,
            RuntimeErrorData.Present(null)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(absent.ErrorData.IsPresent, Is.False);
            Assert.That(presentNull.ErrorData.IsPresent, Is.True);
            Assert.That(presentNull.ErrorData.Value, Is.Null);
        }
    }
}
