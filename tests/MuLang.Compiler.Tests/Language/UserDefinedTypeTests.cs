using MuLang.Compiler.Binding;
using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;
using MuLang.IR.Serialization;
using MuLang.TestSupport;

namespace MuLang.Compiler.Tests.Language;

public sealed class UserDefinedTypeTests
{
    [Test]
    public void BindsForwardAndMutuallyRecursiveDeclarations()
    {
        BindingResult result = BindProgram(
            """
            type Left { right?: Right };
            type Right { left?: Left };
            var left: Left = { right?: Right };
            var right: Right = { left?: Left };
            """
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void BindsNestedInlineTypesAndSuffixes()
    {
        BindingResult result = BindProgram(
            """
            var values: { item: @{ name$: string }? }[]$ = $[
                { item: @{ name$: string }? = null }
            ];
            """
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void UsesStructuralCompatibilityAcrossNamesAndInlineTypes()
    {
        BindingResult result = BindProgram(
            """
            type First { value: int };
            type Second { value: int };
            var first: First = { value: int = 1 };
            var second: Second = first;
            var inline: { value: int } = second;
            """
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void KeepsTypeAndValueNamespacesSeparate()
    {
        BindingResult result = BindProgram(
            """
            type Value { value: int };
            var Value: Value = { value: int = 1 };
            """
        );

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ReportsDeclarationConflictsAndDuplicateProperties()
    {
        ObjectTypeSymbol host = new(
            "type.host",
            "Host",
            false,
            [new ObjectPropertySymbol("value", TypeSymbols.Int)]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(host)
            .Build(LanguageVersion.Version1_2);
        BindingResult result = BindProgram(
            """
            type Host { value: int };
            type Duplicate { value: int };
            type Duplicate { value: int };
            type Properties { value: int, value: string };
            """,
            environment
        );

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.TypeConflict)
                .And.Contain(DiagnosticCodes.DuplicateType)
                .And.Contain(DiagnosticCodes.DuplicateObjectProperty)
        );
    }

    [Test]
    public void ReportsDisabledNamedAndInlineTypesWithoutSyntaxCascades()
    {
        LanguageProfile profile = new LanguageProfileBuilder(
                LanguageProfiles.Version1_2
            )
            .WithUserDefinedTypes(UserDefinedTypesFeature.Disabled)
            .Build();
        BindingResult result = BindProgram(
            """
            type Item { value: int };
            var item: { value: int } = { value: int = 1 };
            """,
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            profile
        );

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Is.EqualTo(
                [
                    DiagnosticCodes.DisabledTypeDeclarations,
                    DiagnosticCodes.DisabledInlineObjectTypes,
                ]
            )
        );
    }

    [Test]
    public void RejectsTypeDeclarationsInExpressionMode()
    {
        AnalysisResult result = MuLangCompiler.Analyze(
            "type Item { value: int };",
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            CompilationMode.Expression,
            profile: LanguageProfiles.Version1_2
        );

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.TypeDeclarationNotAllowed)
        );
    }

    [Test]
    public void LowersSourceTypesStructurallyWithoutSourceNames()
    {
        CompilationResult result = MuLangCompiler.Compile(
            """
            type Point { x: int, y: int };
            var point: Point = { x: int = 1, y: int = 2 };
            """,
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            CompilationMode.Program,
            profile: LanguageProfiles.Version1_2
        );
        IrSlot point = result.Program?.Slots.Single(
            static slot => slot is { Kind: IrSlotKind.Local, Name: "point" }
        ) ?? throw new AssertionException("Expected a point slot.");
        ObjectTypeSymbol type = (ObjectTypeSymbol)point.Type;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(type.Id, Is.Null);
            Assert.That(type.Name, Is.EqualTo("<anonymous>"));
            Assert.That(
                type.Properties.Select(static property => property.Name),
                Is.EquivalentTo(["x", "y"])
            );
        }
    }

    [Test]
    public void ProjectsRecursiveSourceDeclarations()
    {
        CompilationResult result = CompileProgram(
            """
            type Node { next?: Node };
            var node: Node = { next?: Node };
            """
        );
        ObjectTypeSymbol node = (ObjectTypeSymbol)(
            result.Program?.Slots.Single(
                static slot => slot is { Kind: IrSlotKind.Local, Name: "node" }
            ).Type ?? throw new AssertionException("Expected a node slot.")
        );

        Assert.That(
            node.Properties.Single().Type,
            Is.SameAs(node)
        );
    }

    [Test]
    public void ErasesSourceNamesAndUnusedDeclarationsFromMuIr()
    {
        IrProgram firstReachable = CompileProgram(
            """
            type First { value: int };
            var item: First = { value: int = 1 };
            """
        ).Program ?? throw new AssertionException("Expected an IR program.");
        IrProgram otherReachable = CompileProgram(
            """
            type Other { value: int };
            var item: Other = { value: int = 1 };
            """
        ).Program ?? throw new AssertionException("Expected an IR program.");
        IrProgram firstUnused = CompileProgram(
            """
            type Unused { first: int };
            var value = 1;
            """
        ).Program ?? throw new AssertionException("Expected an IR program.");
        IrProgram otherUnused = CompileProgram(
            """
            type Unused { other: int };
            var value = 1;
            """
        ).Program ?? throw new AssertionException("Expected an IR program.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                MuIrWriter.WriteToString(firstReachable),
                Is.EqualTo(MuIrWriter.WriteToString(otherReachable))
            );
            Assert.That(
                MuIrWriter.WriteToString(firstUnused),
                Is.EqualTo(MuIrWriter.WriteToString(otherUnused))
            );
        }
    }

    private static BindingResult BindProgram(
        string source,
        EnvironmentSchema? environment = null,
        LanguageProfile? profile = null
    )
    {
        profile ??= LanguageProfiles.Version1_2;
        environment ??= new EnvironmentBuilder().Build(profile.LanguageVersion);
        SyntaxTree tree = Parser.Parse(
            SourceText.From(source),
            CompilationMode.Program,
            profile
        );

        return Binder.Bind(tree, environment, TypeSymbols.Void);
    }

    private static CompilationResult CompileProgram(string source)
    {
        return MuLangCompiler.Compile(
            source,
            new EnvironmentBuilder().Build(LanguageVersion.Version1_2),
            CompilationMode.Program,
            profile: LanguageProfiles.Version1_2
        );
    }
}
