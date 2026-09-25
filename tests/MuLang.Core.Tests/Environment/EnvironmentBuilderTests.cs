using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.Core.Tests.Environment;

public sealed class EnvironmentBuilderTests
{
    [Test]
    public void UsesLanguageVersionOneOneByDefault()
    {
        EnvironmentSchema schema = new EnvironmentBuilder().Build();

        Assert.That(schema.LanguageVersion, Is.EqualTo(LanguageVersion.Version1_1));
    }

    [Test]
    public void BuildsLookupTables()
    {
        ObjectTypeSymbol itemType = CreateItemType();
        EnvironmentSchema schema = new EnvironmentBuilder()
            .AddType(itemType)
            .AddGlobal("global.items", "items", TypeSymbols.Array(itemType))
            .AddFunction(
                "function.log",
                "log",
                [ new ParameterSymbol("value", TypeSymbols.Unknown) ],
                TypeSymbols.Void
            )
            .Build();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schema.TryGetType("Item", out ObjectTypeSymbol? resolvedType), Is.True);
            Assert.That(resolvedType, Is.SameAs(itemType));
            Assert.That(schema.TryGetGlobal("items", out _), Is.True);
            Assert.That(schema.TryGetFunction("log", out _), Is.True);
        }
    }

    [Test]
    public void AllowsFunctionAndGlobalToUseTheSameName()
    {
        EnvironmentSchema schema = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .AddFunction("function.value", "value", [ ], TypeSymbols.Int)
            .Build();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schema.TryGetGlobal("value", out _), Is.True);
            Assert.That(schema.TryGetFunction("value", out _), Is.True);
        }
    }

    [Test]
    public void RejectsDuplicatesAndReservedNames()
    {
        EnvironmentBuilder builder = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int);

        Assert.That(
            () => builder.AddGlobal("global.other", "value", TypeSymbols.Int),
            Throws.ArgumentException
        );
        Assert.That(
            static () => new EnvironmentBuilder().AddGlobal(
                "global.if",
                "if",
                TypeSymbols.Int
            ),
            Throws.ArgumentException
        );
    }

    [Test]
    public void ReservesNonFiniteLiteralNamesOnlyInVersionOneOne()
    {
        IReadOnlyList<Func<EnvironmentBuilder>> builderFactories =
        [
            static () => new EnvironmentBuilder().AddType(
                new ObjectTypeSymbol("type.infty", "infty", false, [ ])
            ),
            static () => new EnvironmentBuilder().AddGlobal(
                "global.infty",
                "infty",
                TypeSymbols.Float
            ),
            static () => new EnvironmentBuilder().AddFunction(
                "function.nan",
                "nan",
                [ ],
                TypeSymbols.Float
            ),
            static () => new EnvironmentBuilder().AddFunction(
                "function.value",
                "value",
                [ new ParameterSymbol("nan", TypeSymbols.Float) ],
                TypeSymbols.Float
            ),
        ];

        foreach (Func<EnvironmentBuilder> createBuilder in builderFactories)
        {
            Assert.That(
                () => createBuilder().Build(LanguageVersion.Version1),
                Throws.Nothing
            );
            Assert.That(
                () => createBuilder().Build(LanguageVersion.Version1_1),
                Throws.InvalidOperationException
            );
        }
    }

    [Test]
    public void ProducesOrderIndependentFingerprint()
    {
        ObjectTypeSymbol firstType = CreateItemType();
        ObjectTypeSymbol secondType = CreateItemType();
        EnvironmentSchema first = new EnvironmentBuilder()
            .AddType(firstType)
            .AddGlobal("global.items", "items", TypeSymbols.Array(firstType))
            .AddFunction("function.count", "count", [ ], TypeSymbols.Int)
            .Build();
        EnvironmentSchema second = new EnvironmentBuilder()
            .AddFunction("function.count", "count", [ ], TypeSymbols.Int)
            .AddGlobal("global.items", "items", TypeSymbols.Array(secondType))
            .AddType(secondType)
            .Build();

        Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
    }

    [Test]
    public void ChangesFingerprintWhenSchemaChanges()
    {
        EnvironmentSchema first = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Int)
            .Build();
        EnvironmentSchema second = new EnvironmentBuilder()
            .AddGlobal("global.value", "value", TypeSymbols.Number)
            .Build();

        Assert.That(second.Fingerprint, Is.Not.EqualTo(first.Fingerprint));
    }

    [Test]
    public void DistinguishesArrayCapabilityInFingerprint()
    {
        EnvironmentSchema mutable = new EnvironmentBuilder()
            .AddGlobal("global.values", "values", TypeSymbols.Array(TypeSymbols.Int))
            .Build(LanguageVersion.Version1_1);
        EnvironmentSchema readOnly = new EnvironmentBuilder()
            .AddGlobal(
                "global.values",
                "values",
                TypeSymbols.ReadOnlyArray(TypeSymbols.Int)
            )
            .Build(LanguageVersion.Version1_1);

        Assert.That(readOnly.Fingerprint, Is.Not.EqualTo(mutable.Fingerprint));
    }

    [Test]
    public void IncludesStructuredTypeShapeInFingerprint()
    {
        ObjectTypeSymbol requiredPropertyType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int) ]
        );
        ObjectTypeSymbol optionalPropertyType = new (
            "type.item",
            "Item",
            false,
            [ new ObjectPropertySymbol("value", TypeSymbols.Int, true) ]
        );
        EnvironmentSchema first = new EnvironmentBuilder()
            .AddType(requiredPropertyType)
            .AddGlobal("global.item", "item", requiredPropertyType)
            .Build();
        EnvironmentSchema second = new EnvironmentBuilder()
            .AddType(optionalPropertyType)
            .AddGlobal("global.item", "item", optionalPropertyType)
            .Build();

        Assert.That(second.Fingerprint, Is.Not.EqualTo(first.Fingerprint));
    }

    [Test]
    public void RejectsUnregisteredStructuredTypes()
    {
        ObjectTypeSymbol itemType = CreateItemType();
        EnvironmentBuilder builder = new EnvironmentBuilder()
            .AddGlobal("global.item", "item", itemType);

        Assert.That(() => builder.Build(), Throws.InvalidOperationException);
    }

    [Test]
    public void RejectsDifferentInstanceForRegisteredTypeIdentifier()
    {
        ObjectTypeSymbol registeredType = CreateItemType();
        ObjectTypeSymbol referencedType = CreateItemType();
        EnvironmentBuilder builder = new EnvironmentBuilder()
            .AddType(registeredType)
            .AddGlobal("global.item", "item", referencedType);

        Assert.That(() => builder.Build(), Throws.InvalidOperationException);
    }

    [Test]
    public void AllowsNonIdentifierObjectPropertyNames()
    {
        ObjectPropertySymbol property = new ("display-name", TypeSymbols.String);

        Assert.That(property.Name, Is.EqualTo("display-name"));
    }

    private static ObjectTypeSymbol CreateItemType()
    {
        return new ObjectTypeSymbol(
            "type.item",
            "Item",
            false,
            [
                new ObjectPropertySymbol("id", TypeSymbols.Int),
                new ObjectPropertySymbol("label", TypeSymbols.String, true),
            ]
        );
    }
}
