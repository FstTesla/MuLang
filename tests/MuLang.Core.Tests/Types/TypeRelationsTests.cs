using MuLang.Core.Types;

namespace MuLang.Core.Tests.Types;

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
    public void AppliesPrimitiveAssignabilityAndCastability()
    {
        IReadOnlyList<TypeSymbol> concreteTypes =
        [
            TypeSymbols.Bool,
            TypeSymbols.Int,
            TypeSymbols.Float,
            TypeSymbols.Number,
            TypeSymbols.String,
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                concreteTypes,
                Has.All.Matches<TypeSymbol>(
                    static type => TypeRelations.IsAssignable(type, TypeSymbols.Primitive)
                )
            );
            Assert.That(
                concreteTypes,
                Has.All.Matches<TypeSymbol>(
                    static type => TypeRelations.IsCastable(TypeSymbols.Primitive, type)
                )
            );
            Assert.That(
                TypeRelations.IsAssignable(TypeSymbols.Primitive, TypeSymbols.Unknown),
                Is.True
            );
            Assert.That(
                TypeRelations.IsAssignable(TypeSymbols.Object, TypeSymbols.Primitive),
                Is.False
            );
            Assert.That(
                TypeRelations.IsAssignable(
                    TypeSymbols.Array(TypeSymbols.Int),
                    TypeSymbols.Primitive
                ),
                Is.False
            );
        }
    }

    [Test]
    public void AppliesNullableAndReadOnlyPrimitiveWidening()
    {
        TypeSymbol nullableInt = TypeSymbols.Nullable(TypeSymbols.Int);
        TypeSymbol nullablePrimitive = TypeSymbols.Nullable(TypeSymbols.Primitive);
        TypeSymbol mutableInts = TypeSymbols.Array(TypeSymbols.Int);
        TypeSymbol readOnlyPrimitives = TypeSymbols.ReadOnlyArray(
            TypeSymbols.Primitive
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.IsAssignable(nullableInt, nullablePrimitive),
                Is.True
            );
            Assert.That(
                TypeRelations.IsAssignable(mutableInts, readOnlyPrimitives),
                Is.True
            );
            Assert.That(
                TypeRelations.IsAssignable(
                    mutableInts,
                    TypeSymbols.Array(TypeSymbols.Primitive)
                ),
                Is.False
            );
        }
    }

    [Test]
    public void FindsPrimitiveCommonTypes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Bool, TypeSymbols.String),
                Is.SameAs(TypeSymbols.Primitive)
            );
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Int, TypeSymbols.String),
                Is.SameAs(TypeSymbols.Primitive)
            );
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Int, TypeSymbols.Float),
                Is.SameAs(TypeSymbols.Float)
            );
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Object, TypeSymbols.String),
                Is.Null
            );
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
    public void AppliesReadOnlyArrayCovariance()
    {
        TypeSymbol intArray = TypeSymbols.Array(TypeSymbols.Int);
        TypeSymbol readOnlyIntArray = TypeSymbols.ReadOnlyArray(TypeSymbols.Int);
        TypeSymbol readOnlyNumberArray = TypeSymbols.ReadOnlyArray(TypeSymbols.Number);
        TypeSymbol readOnlyFloatArray = TypeSymbols.ReadOnlyArray(TypeSymbols.Float);
        TypeSymbol readOnlyUnknownArray = TypeSymbols.ReadOnlyArray(TypeSymbols.Unknown);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeRelations.IsAssignable(intArray, readOnlyIntArray), Is.True);
            Assert.That(TypeRelations.IsAssignable(intArray, readOnlyNumberArray), Is.True);
            Assert.That(
                TypeRelations.IsAssignable(readOnlyIntArray, readOnlyNumberArray),
                Is.True
            );
            Assert.That(
                TypeRelations.IsAssignable(readOnlyIntArray, readOnlyUnknownArray),
                Is.True
            );
            Assert.That(
                TypeRelations.IsAssignable(readOnlyIntArray, readOnlyFloatArray),
                Is.False
            );
            Assert.That(TypeRelations.IsAssignable(readOnlyIntArray, intArray), Is.False);
        }
    }

    [Test]
    public void AppliesNestedReadOnlyArrayCovariance()
    {
        TypeSymbol mutableNested = TypeSymbols.Array(
            TypeSymbols.Array(TypeSymbols.Int)
        );
        TypeSymbol readOnlyNested = TypeSymbols.ReadOnlyArray(
            TypeSymbols.ReadOnlyArray(TypeSymbols.Number)
        );

        Assert.That(
            TypeRelations.IsAssignable(mutableNested, readOnlyNested),
            Is.True
        );
    }

    [Test]
    public void DistinguishesArrayCapabilityInEquivalenceAndConversions()
    {
        TypeSymbol mutable = TypeSymbols.Array(TypeSymbols.Int);
        TypeSymbol readOnly = TypeSymbols.ReadOnlyArray(TypeSymbols.Int);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeRelations.AreEquivalent(mutable, readOnly), Is.False);
            Assert.That(
                TypeRelations.ClassifyConversion(mutable, readOnly),
                Is.EqualTo(ConversionKind.Implicit)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(readOnly, mutable),
                Is.EqualTo(ConversionKind.None)
            );
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
            Assert.That(
                TypeRelations.ClassifyConversion(source, openTarget),
                Is.EqualTo(ConversionKind.None)
            );
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
    public void AppliesReadOnlyObjectPropertyViews()
    {
        ObjectTypeSymbol mutableInt = CreateObject(
            "mutable-int",
            "MutableInt",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int)
        );
        ObjectTypeSymbol readOnlyNumber = CreateObject(
            "readonly-number",
            "ReadOnlyNumber",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Number, false, true)
        );
        ObjectTypeSymbol readOnlyInt = CreateObject(
            "readonly-int",
            "ReadOnlyInt",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int, false, true)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.IsAssignable(mutableInt, readOnlyNumber),
                Is.True
            );
            Assert.That(
                TypeRelations.IsAssignable(readOnlyInt, mutableInt),
                Is.False
            );
            Assert.That(
                TypeRelations.AreEquivalent(mutableInt, readOnlyInt),
                Is.False
            );
        }
    }

    [Test]
    public void AppliesRecursiveReadOnlyObjectPropertyViewsCoinductively()
    {
        ObjectTypeSymbol mutable = CreateRecursiveObject(false);
        ObjectTypeSymbol readOnly = CreateRecursiveObject(true);

        Assert.That(TypeRelations.IsAssignable(mutable, readOnly), Is.True);

        static ObjectTypeSymbol CreateRecursiveObject(bool isReadOnly)
        {
            ObjectTypeGraphBuilder builder = new ();
            ObjectTypeGraphReference node = builder.DeclareAnonymous(
                "node",
                false
            );
            builder.AddProperty(node, "next", node, false, isReadOnly);
            return (ObjectTypeSymbol)builder.Build()[node];
        }
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
                Is.EqualTo(ConversionKind.None)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(TypeSymbols.Number, TypeSymbols.Float),
                Is.EqualTo(ConversionKind.None)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(TypeSymbols.Int, TypeSymbols.Float),
                Is.EqualTo(ConversionKind.Implicit)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(TypeSymbols.Float, TypeSymbols.Number),
                Is.EqualTo(ConversionKind.Implicit)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(TypeSymbols.Float, TypeSymbols.Int),
                Is.EqualTo(ConversionKind.None)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(nullableInt, TypeSymbols.Int),
                Is.EqualTo(ConversionKind.Checked)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(nullableObject, TypeSymbols.String),
                Is.EqualTo(ConversionKind.None)
            );
            Assert.That(
                TypeRelations.ClassifyConversion(TypeSymbols.Unknown, TypeSymbols.Int),
                Is.EqualTo(ConversionKind.None)
            );
        }
    }

    [Test]
    public void DistinguishesCheckedCastsFromTransformingConversions()
    {
        TypeSymbol nullableInt = TypeSymbols.Nullable(TypeSymbols.Int);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TypeRelations.IsCastable(TypeSymbols.Int, TypeSymbols.Float), Is.False);
            Assert.That(TypeRelations.IsCastable(TypeSymbols.Int, TypeSymbols.Number), Is.True);
            Assert.That(TypeRelations.IsCastable(TypeSymbols.Number, TypeSymbols.Int), Is.True);
            Assert.That(TypeRelations.IsCastable(TypeSymbols.Number, TypeSymbols.Float), Is.True);
            Assert.That(TypeRelations.IsCastable(TypeSymbols.Bool, TypeSymbols.String), Is.False);
            Assert.That(TypeRelations.IsCastable(TypeSymbols.Null, TypeSymbols.String), Is.False);
            Assert.That(TypeRelations.IsCastable(TypeSymbols.Unknown, TypeSymbols.String), Is.True);
            Assert.That(TypeRelations.IsCastable(nullableInt, TypeSymbols.Int), Is.True);
        }
    }

    [Test]
    public void RecognizesGuaranteedShapeBasedArrayConformance()
    {
        TypeSymbol mutableInts = TypeSymbols.Array(TypeSymbols.Int);
        TypeSymbol mutableNumbers = TypeSymbols.Array(TypeSymbols.Number);
        TypeSymbol mutableFloats = TypeSymbols.Array(TypeSymbols.Float);
        TypeSymbol readOnlyInts = TypeSymbols.ReadOnlyArray(TypeSymbols.Int);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.IsConformanceGuaranteed(
                    mutableInts,
                    mutableNumbers
                ),
                Is.True
            );
            Assert.That(
                TypeRelations.IsConformanceGuaranteed(
                    mutableInts,
                    mutableFloats
                ),
                Is.False
            );
            Assert.That(
                TypeRelations.IsConformanceGuaranteed(
                    readOnlyInts,
                    mutableNumbers
                ),
                Is.False
            );
        }
    }

    [Test]
    public void RecognizesGuaranteedStructuralObjectConformance()
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
            new ObjectPropertySymbol("value", TypeSymbols.Number)
        );
        ObjectTypeSymbol closedTarget = CreateObject(
            "closed-target",
            "ClosedTarget",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Number)
        );
        ObjectTypeSymbol optionalTarget = CreateObject(
            "optional-target",
            "OptionalTarget",
            false,
            new ObjectPropertySymbol("value", TypeSymbols.Int),
            new ObjectPropertySymbol(
                "label",
                TypeSymbols.String,
                true
            )
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                TypeRelations.IsConformanceGuaranteed(source, openTarget),
                Is.True
            );
            Assert.That(
                TypeRelations.IsConformanceGuaranteed(source, closedTarget),
                Is.False
            );
            Assert.That(
                TypeRelations.IsConformanceGuaranteed(source, optionalTarget),
                Is.True
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
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Int, TypeSymbols.Float),
                Is.SameAs(TypeSymbols.Float)
            );
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Float, TypeSymbols.Number),
                Is.SameAs(TypeSymbols.Number)
            );
            Assert.That(TypeRelations.AreEquivalent(nullCommonType, nullableInt), Is.True);
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Void, TypeSymbols.Int),
                Is.Null
            );
            Assert.That(
                TypeRelations.GetCommonType(TypeSymbols.Bool, TypeSymbols.String),
                Is.SameAs(TypeSymbols.Primitive)
            );
        }
    }

    [Test]
    public void FindsReadOnlyCommonArrayTypes()
    {
        TypeSymbol mutableInts = TypeSymbols.Array(TypeSymbols.Int);
        TypeSymbol readOnlyFloats = TypeSymbols.ReadOnlyArray(TypeSymbols.Float);
        TypeSymbol common =
            TypeRelations.GetCommonType(mutableInts, readOnlyFloats) ??
            throw new AssertionException("Expected a common type.");

        Assert.That(
            TypeRelations.AreEquivalent(
                common,
                TypeSymbols.ReadOnlyArray(TypeSymbols.Number)
            ),
            Is.True
        );
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
        params IEnumerable<ObjectPropertySymbol> properties
    )
    {
        return new ObjectTypeSymbol(id, name, isOpen, properties);
    }
}
