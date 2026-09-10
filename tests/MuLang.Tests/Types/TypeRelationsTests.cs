using MuLang.Core.Types;

namespace MuLang.Tests.Types;

public sealed class TypeRelationsTests
{
    [Test]
    public void AppliesUnknownAndNullableAssignability()
    {
        TypeSymbol nullableInt = TypeSymbols.Nullable(TypeSymbols.Int);
        TypeSymbol nullableUnknown = TypeSymbols.Nullable(TypeSymbols.Unknown);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeRelations.IsAssignable(TypeSymbols.Int, TypeSymbols.Unknown), Is.True);
            Assert.That(TypeRelations.IsAssignable(nullableInt, TypeSymbols.Unknown), Is.False);
            Assert.That(TypeRelations.IsAssignable(nullableInt, nullableUnknown), Is.True);
            Assert.That(TypeRelations.IsAssignable(TypeSymbols.Null, nullableInt), Is.True);
        }
    }

    [Test]
    public void KeepsMutableArraysInvariant()
    {
        TypeSymbol intArray = TypeSymbols.Array(TypeSymbols.Int);
        TypeSymbol numberArray = TypeSymbols.Array(TypeSymbols.Number);
        TypeSymbol unknownArray = TypeSymbols.Array(TypeSymbols.Unknown);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeRelations.IsAssignable(intArray, numberArray), Is.False);
            Assert.That(TypeRelations.IsAssignable(intArray, unknownArray), Is.False);
        }
    }

    [Test]
    public void AppliesStructuralObjectCompatibility()
    {
        ObjectTypeSymbol source = CreateObject(
            "source",
            "Source",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int),
            new ObjectPropertySymbol("label", TypeSymbols.String)
        );
        ObjectTypeSymbol openTarget = CreateObject(
            "open-target",
            "OpenTarget",
            true,
            new ObjectPropertySymbol("value", TypeSymbols.Int)
        );
        ObjectTypeSymbol equivalentOpenSource = CreateObject(
            "open-source",
            "OpenSource",
            true,
            new ObjectPropertySymbol("value", TypeSymbols.Int)
        );
        ObjectTypeSymbol closedTarget = CreateObject(
            "closed-target",
            "ClosedTarget",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeRelations.IsAssignable(source, openTarget), Is.False);
            Assert.That(TypeRelations.IsAssignable(equivalentOpenSource, openTarget), Is.True);
            Assert.That(TypeRelations.IsAssignable(source, closedTarget), Is.False);
            Assert.That(TypeRelations.IsAssignable(source, TypeSymbols.Object), Is.True);
        }
    }

    [Test]
    public void RequiresInvariantMutablePropertyTypes()
    {
        ObjectTypeSymbol intObject = CreateObject(
            "int-object",
            "IntObject",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int)
        );
        ObjectTypeSymbol numberObject = CreateObject(
            "number-object",
            "NumberObject",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Number)
        );

        Assert.That(TypeRelations.IsAssignable(intObject, numberObject), Is.False);
    }

    [Test]
    public void DistinguishesAssignabilityFromCheckedConversion()
    {
        TypeSymbol nullableObject = TypeSymbols.Nullable(TypeSymbols.Object);
        TypeSymbol nullableInt = TypeSymbols.Nullable(TypeSymbols.Int);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.ClassifyConversion(TypeSymbols.Number, TypeSymbols.Int),
                Is.EqualTo(ConversionKind.Checked)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(nullableInt, TypeSymbols.Int),
                Is.EqualTo(ConversionKind.Checked)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(nullableObject, TypeSymbols.String),
                Is.EqualTo(ConversionKind.None)
            );
        }
    }

    [Test]
    public void FindsCommonTypesForNumbersAndNullability()
    {
        TypeSymbol nullableInt = TypeSymbols.Nullable(TypeSymbols.Int);
        TypeSymbol nullCommonType =
            TypeRelations.GetCommonType(TypeSymbols.Null, nullableInt) ??
            throw new AssertionException("Expected a common type.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Int, TypeSymbols.Number),
                Is.SameAs(TypeSymbols.Number)
            );
            Assert.That(TypeRelations.AreEquivalent(nullCommonType, nullableInt), Is.True);
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Void, TypeSymbols.Int),
                Is.Null
            );
        }
    }

    [Test]
    public void RejectsClosedObjectWithDifferentExtraProperty()
    {
        ObjectTypeSymbol source = CreateObject(
            "source",
            "Source",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int),
            new ObjectPropertySymbol("other", TypeSymbols.Bool)
        );
        ObjectTypeSymbol target = CreateObject(
            "target",
            "Target",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int),
            new ObjectPropertySymbol("extra", TypeSymbols.String, true)
        );

        Assert.That(TypeRelations.IsAssignable(source, target), Is.False);
    }

    private static ObjectTypeSymbol CreateObject(
        string id,
        string name,
        bool isOpen,
        params ObjectPropertySymbol[] properties
    )
    {
        return new ObjectTypeSymbol(id, name, isOpen, properties);
    }
}
