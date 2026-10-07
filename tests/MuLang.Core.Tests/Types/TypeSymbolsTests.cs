using MuLang.Core.Types;

namespace MuLang.Core.Tests.Types;

public sealed class TypeSymbolsTests
{
    [Test]
    public void FormatsNestedArrayAndNullableTypes()
    {
        TypeSymbol nullableElements = TypeSymbols.Array(TypeSymbols.Nullable(TypeSymbols.Int));
        TypeSymbol nullableArray = TypeSymbols.Nullable(TypeSymbols.Array(TypeSymbols.Int));
        TypeSymbol readOnlyNullableElements = TypeSymbols.ReadOnlyArray(
            TypeSymbols.Nullable(TypeSymbols.Int)
        );
        TypeSymbol nullableReadOnlyArray = TypeSymbols.Nullable(
            TypeSymbols.ReadOnlyArray(TypeSymbols.Int)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nullableElements.DisplayName, Is.EqualTo("int?[]"));
            Assert.That(nullableArray.DisplayName, Is.EqualTo("int[]?"));
            Assert.That(readOnlyNullableElements.DisplayName, Is.EqualTo("int?[]$"));
            Assert.That(nullableReadOnlyArray.DisplayName, Is.EqualTo("int[]$?"));
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
    public void ExposesPrimitiveType()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeSymbols.Primitive.Kind, Is.EqualTo(TypeKind.Primitive));
            Assert.That(TypeSymbols.Primitive.DisplayName, Is.EqualTo("primitive"));
        }
    }

    [Test]
    public void ExposesErrorValueAndRecoveryAliases()
    {
#pragma warning disable CS0618
        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeSymbols.ErrorValue.Kind, Is.EqualTo(TypeKind.ErrorValue));
            Assert.That(TypeSymbols.ErrorValue.DisplayName, Is.EqualTo("error"));
            Assert.That(TypeSymbols.Error, Is.SameAs(TypeSymbols.ErrorRecovery));
            Assert.That(TypeKind.Error, Is.EqualTo(TypeKind.ErrorRecovery));
        }
#pragma warning restore CS0618
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
            static () => TypeSymbols.ReadOnlyArray(TypeSymbols.Void),
            Throws.ArgumentException
        );
        Assert.That(
            static () => TypeSymbols.Nullable(TypeSymbols.Nullable(TypeSymbols.Int)),
            Throws.ArgumentException
        );
    }
}
