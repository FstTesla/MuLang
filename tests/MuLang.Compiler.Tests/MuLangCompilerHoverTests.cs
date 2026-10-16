using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Tests;

public sealed class MuLangCompilerHoverTests
{
    [TestCase("var |value = 1;", "var value: int", "value")]
    [TestCase("var value$ = 1; |value;", "var value$: int", "value")]
    [TestCase("var value = 1; { var value = \"text\"; |value; }", "var value: string", "value")]
    [TestCase("var value = 1; { var value = \"text\"; } |value;", "var value: int", "value")]
    [TestCase("func |identity(value: int): int { return value; }", "func identity(value: int): int", "identity")]
    [TestCase("func identity(value: int): int { return value; } |identity(1);", "func identity(value: int): int", "identity")]
    [TestCase("func identity(|value: int): int { return value; }", "value: int", "value")]
    [TestCase("func identity(value: int): int { return |value; }", "value: int", "value")]
    [TestCase("func |run(): void { }", "func run(): void", "run")]
    [TestCase("var value: |int = 1;", "int", "int")]
    [TestCase("type |Node { next?: Node };", "type Node { next?: Node }", "Node")]
    [TestCase("type Node { next?: |Node };", "type Node { next?: Node }", "Node")]
    [TestCase("type Node { |next?: Node };", "next?: Node", "next")]
    [TestCase("type Node { next?: Node }; var value: |Node?;", "type Node { next?: Node }", "Node")]
    [TestCase("var value: { |count$: int } = { count$ = 1 };", "count$: int", "count")]
    [TestCase("var value = { |count$ = 1 };", "count$: int", "count")]
    [TestCase("var value = { |\"first-name\"$ = \"text\" };", "\"first-name\"$: string", "\"first-name\"")]
    [TestCase("var value = { |\"line\\nbreak\" = 1 };", "\"line\\nbreak\": int", "\"line\\nbreak\"")]
    [TestCase("var value = { \"first-name\"$ = \"text\" }; value[|\"first-name\"];", "\"first-name\"$: string", "\"first-name\"")]
    [TestCase("var value = { count$ = 1 }; value.|count;", "count$: int", "count")]
    [TestCase("var value = { count$ = 1 }; value[|\"count\"];", "count$: int", "\"count\"")]
    [TestCase("var |value = @{ count? = 1 };", "var value: @{ count?: int }", "value")]
    [TestCase("var |values = $[1, 2];", "var values: int[]$", "values")]
    [TestCase("type Node @{ name$: string, next?: Node }; var |nodes: Node?[]$;", "var nodes: Node?[]$", "nodes")]
    [TestCase("func run(): void { var value: { |count?: int }; }", "count?: int", "count")]
    [TestCase("type Node { next?: Node }; func run(value: |Node): void { }", "type Node { next?: Node }", "Node")]
    [TestCase("var values = [1, 2]; values.|length;", "length$: int", "length")]
    [TestCase("var value = 1; var result = true ? |value : 0;", "var value: int", "value")]
    [TestCase("var value = 1; while (false) { |value; }", "var value: int", "value")]
    [TestCase("var value = 1; for (var index = 0; index < 1; index = index + 1) { |value; }", "var value: int", "value")]
    [TestCase("try { } catch (|failure) { failure.message; }", "var failure$: error", "failure")]
    [TestCase("try { } catch (failure) { |failure.message; }", "var failure$: error", "failure")]
    [TestCase("try { } catch (failure) { failure.|message; }", "message$: string", "message")]
    [TestCase("var value = 1; try { } finally { |value; }", "var value: int", "value")]
    [TestCase("try { } catch (failure) { throw |failure; }", "var failure$: error", "failure")]
    [TestCase("var value = 1; |value; missing;", "var value: int", "value")]
    [TestCase("var value = 1; (|value);", "var value: int", "value")]
    public void ReturnsBoundSymbolSignatures(
        string markedSource,
        string content,
        string token
    )
    {
        (string source, int offset) = GetPosition(markedSource);
        HoverInfo? hover = MuLangCompiler.GetHover(
            source,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program,
            offset
        );

        Assert.That(hover, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hover!.Content, Is.EqualTo(content));
            Assert.That(SourceText.From(source).GetText(hover.Span), Is.EqualTo(token));
            Assert.That(hover.Span.Start, Is.EqualTo(offset));
        }
    }

    [TestCase("|customer", "var customer$: Customer", "customer")]
    [TestCase("|load(customer)", "func load(value: Customer): Customer", "load")]
    [TestCase("customer.|name", "name$?: string", "name")]
    [TestCase("customer is |Customer", "type Customer { name$?: string }", "Customer")]
    public void ReturnsConfiguredHostSymbols(
        string markedSource,
        string content,
        string token
    )
    {
        ObjectTypeSymbol customer = new (
            "customer",
            "Customer",
            false,
            [ new ObjectPropertySymbol("name", TypeSymbols.String, true, true) ]
        );
        EnvironmentSchema environment = new EnvironmentBuilder()
            .AddType(customer)
            .AddGlobal("customer", "customer", customer)
            .AddFunction(
                "load",
                "load",
                [ new ParameterSymbol("value", customer) ],
                customer
            )
            .Build();
        (string source, int offset) = GetPosition(markedSource);
        HoverInfo? hover = MuLangCompiler.GetHover(
            source,
            environment,
            CompilationMode.Expression,
            offset,
            new MuLangCompilationOptions
            {
                Profile = LanguageProfiles.Version1_2,
            }
        );

        Assert.That(hover, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hover!.Content, Is.EqualTo(content));
            Assert.That(SourceText.From(source).GetText(hover.Span), Is.EqualTo(token));
        }
    }

    [TestCase("|var value = 1;")]
    [TestCase("var value| = 1;")]
    [TestCase("var value = |1;")]
    [TestCase("var value = 1;|")]
    [TestCase("|missing;")]
    [TestCase("var value: |Missing;")]
    [TestCase("var value = |missing();")]
    [TestCase("var value = 1; |(value);")]
    [TestCase("var |value = missing;")]
    [TestCase("var value = @{ }; value.|missing;")]
    [TestCase("|")]
    public void OmitsUnresolvedSymbolsAndNonSymbolPositions(string markedSource)
    {
        (string source, int offset) = GetPosition(markedSource);
        HoverInfo? hover = MuLangCompiler.GetHover(
            source,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program,
            offset
        );

        Assert.That(hover, Is.Null);
    }

    [Test]
    public void PreservesUnicodeScalarSpans()
    {
        (string source, int offset) = GetPosition("\"😀\";\r\nvar café = 1; ca|fé;");
        HoverInfo? hover = MuLangCompiler.GetHover(
            source,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program,
            offset
        );

        Assert.That(hover, Is.Not.Null);
        Assert.That(hover!.Content, Is.EqualTo("var café: int"));
        Assert.That(SourceText.From(source).GetText(hover.Span), Is.EqualTo("café"));
    }

    [TestCase(-1)]
    [TestCase(2)]
    public void RejectsOutOfBoundsOffsets(int offset)
    {
        Assert.That(
            () => MuLangCompiler.GetHover(
                "",
                new EnvironmentBuilder().Build(),
                CompilationMode.Program,
                offset
            ),
            Throws.TypeOf<ArgumentOutOfRangeException>()
        );
    }

    private static (string Source, int Offset) GetPosition(string markedSource)
    {
        int marker = markedSource.IndexOf('|');
        return (
            markedSource.Remove(marker, 1),
            SourceText.From(markedSource[..marker]).Length
        );
    }
}
