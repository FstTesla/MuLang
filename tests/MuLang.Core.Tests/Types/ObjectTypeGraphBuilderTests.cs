using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.Core.Tests.Types;

public sealed class ObjectTypeGraphBuilderTests
{
    [Test]
    public void BuildsSelfRecursiveObjectAtomically()
    {
        ObjectTypeGraphBuilder builder = new ();
        ObjectTypeGraphReference node = builder.DeclareNamed(
            "node",
            "type.node",
            "Node",
            false
        );
        ObjectTypeGraphReference next = builder.Nullable(node);
        builder.AddProperty(node, "next", next);

        IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> result =
            builder.Build();
        ObjectTypeSymbol type = (ObjectTypeSymbol)result[node];
        NullableTypeSymbol propertyType =
            (NullableTypeSymbol)type.Properties.Single().Type;

        Assert.That(propertyType.UnderlyingType, Is.SameAs(type));
    }

    [Test]
    public void BuildsMutuallyRecursiveEnvironmentTypes()
    {
        (ObjectTypeSymbol first, ObjectTypeSymbol second) = CreatePair();
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(first)
            .AddType(second)
            .Build();
        (ObjectTypeSymbol equivalentFirst, ObjectTypeSymbol equivalentSecond) =
            CreatePair();
        EnvironmentSchema equivalentEnvironment = new EnvironmentBuilder()
            .AddType(equivalentFirst)
            .AddType(equivalentSecond)
            .Build();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(environment.Types, Has.Count.EqualTo(2));
            Assert.That(
                first.Properties.Single().Type,
                Is.SameAs(second)
            );
            Assert.That(
                second.Properties.Single().Type,
                Is.SameAs(first)
            );
            Assert.That(
                TypeRelations.AreEquivalent(first, equivalentFirst),
                Is.True
            );
            Assert.That(
                equivalentEnvironment.Fingerprint,
                Is.EqualTo(environment.Fingerprint)
            );
        }
    }

    [Test]
    public void RejectsInvalidGraphWithoutPublishingTypes()
    {
        ObjectTypeGraphBuilder builder = new ();
        ObjectTypeGraphReference value = builder.DeclareAnonymous("value", false);
        ObjectTypeGraphReference invalid = builder.Nullable(
            builder.From(TypeSymbols.Void)
        );
        builder.AddProperty(value, "invalid", invalid);

        Assert.That(
            () => builder.Build(),
            Throws.TypeOf<ArgumentException>()
        );
    }

    [Test]
    public void RejectsChangesAfterFinalization()
    {
        ObjectTypeGraphBuilder builder = new ();
        _ = builder.DeclareAnonymous("value", false);
        _ = builder.Build();

        Assert.That(
            () => builder.DeclareAnonymous("other", false),
            Throws.TypeOf<InvalidOperationException>()
        );
    }

    private static (ObjectTypeSymbol First, ObjectTypeSymbol Second) CreatePair()
    {
        ObjectTypeGraphBuilder builder = new ();
        ObjectTypeGraphReference first = builder.DeclareNamed(
            "first",
            "type.first",
            "First",
            false
        );
        ObjectTypeGraphReference second = builder.DeclareNamed(
            "second",
            "type.second",
            "Second",
            false
        );
        builder.AddProperty(first, "second", second);
        builder.AddProperty(second, "first", first);
        IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> result =
            builder.Build();
        return (
            (ObjectTypeSymbol)result[first],
            (ObjectTypeSymbol)result[second]
        );
    }
}
