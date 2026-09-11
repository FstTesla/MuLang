using MuLang.Core.Types;

namespace MuLang.Tests.Types;

public sealed class TypeSymbolsTests
{
    [Test]
    public void FormatsNestedArrayAndNullableTypes()
    {
        TypeSymbol nullableElements = TypeSymbols.Array(TypeSymbols.Nullable(TypeSymbols.Int));
        TypeSymbol nullableArray = TypeSymbols.Nullable(TypeSymbols.Array(TypeSymbols.Int));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nullableElements.DisplayName, Is.EqualTo("int?[]"));
            Assert.That(nullableArray.DisplayName, Is.EqualTo("int[]?"));
        }
    }

    [Test]
    public void ExposesFloatAndGenericNumberTypes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeSymbols.Float.DisplayName, Is.EqualTo("float"));
            Assert.That(TypeSymbols.Number.DisplayName, Is.EqualTo("number"));
        }
    }

    [Test]
    public void RejectsInvalidTypeConstructions()
    {
        Assert.That(
            static () => TypeSymbols.Nullable(TypeSymbols.Void),
            Throws.ArgumentException
        );
        Assert.That(
            static () => TypeSymbols.Array(TypeSymbols.Void),
            Throws.ArgumentException
        );
        Assert.That(
            static () => TypeSymbols.Nullable(TypeSymbols.Nullable(TypeSymbols.Int)),
            Throws.ArgumentException
        );
    }
}
