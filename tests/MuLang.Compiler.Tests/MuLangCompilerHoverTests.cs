using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Tests;

public sealed class MuLangCompilerHoverTests
{
    [TestCase("var |value = 1;", "[variable] value: int", "value")]
    [TestCase("var value$ = 1; |value;", "[variable] value$: int", "value")]
    [TestCase("var value = 1; { var value = \"text\"; |value; }", "[variable] value: string", "value")]
    [TestCase("var value = 1; { var value = \"text\"; } |value;", "[variable] value: int", "value")]
    [TestCase("func |identity(value: int): int { return value; }", "[function] identity(value: int): int", "identity")]
    [TestCase("func identity(value: int): int { return value; } |identity(1);", "[function] identity(value: int): int", "identity")]
    [TestCase("func identity(|value: int): int { return value; }", "[parameter] value: int", "value")]
    [TestCase("func identity(value: int): int { return |value; }", "[parameter] value: int", "value")]
    [TestCase("func run(|value: Missing): void { }", "[parameter] value: <unresolved>", "value")]
    [TestCase("func |run(): void { }", "[function] run(): void", "run")]
    [TestCase("var value: |int = 1;", "[type] int", "int")]
    [TestCase("type |Node { next?: Node };", "[type] Node { next?: Node }", "Node")]
    [TestCase("type Node { next?: |Node };", "[type] Node { next?: Node }", "Node")]
    [TestCase("type Node { |next?: Node };", "[property] next?: Node", "next")]
    [TestCase("type Node { next?: Node }; var value: |Node?;", "[type] Node { next?: Node }", "Node")]
    [TestCase("var value: { |count$: int } = { count$ = 1 };", "[property] count$: int", "count")]
    [TestCase("var value = { |count$ = 1 };", "[property] count$: int", "count")]
    [TestCase("var value = { |\"first-name\"$ = \"text\" };", "[property] \"first-name\"$: string", "\"first-name\"")]
    [TestCase("var value = { |\"line\\nbreak\" = 1 };", "[property] \"line\\nbreak\": int", "\"line\\nbreak\"")]
    [TestCase("var value = { \"first-name\"$ = \"text\" }; value[|\"first-name\"];", "[property] \"first-name\"$: string", "\"first-name\"")]
    [TestCase("var value = { count$ = 1 }; value.|count;", "[property] count$: int", "count")]
    [TestCase("var value = { count$ = 1 }; value[|\"count\"];", "[property] count$: int", "\"count\"")]
    [TestCase("var |value = @{ count? = 1 };", "[variable] value: @{ count?: int }", "value")]
    [TestCase("var |values = $[1, 2];", "[variable] values: int[]$", "values")]
    [TestCase("type Node @{ name$: string, next?: Node }; var |nodes: Node?[]$;", "[variable] nodes: Node?[]$", "nodes")]
    [TestCase("func run(): void { var value: { |count?: int }; }", "[property] count?: int", "count")]
    [TestCase("type Node { next?: Node }; func run(value: |Node): void { }", "[type] Node { next?: Node }", "Node")]
    [TestCase("var values = [1, 2]; values.|length;", "[property] length$: int", "length")]
    [TestCase("var value = 1; var result = true ? |value : 0;", "[variable] value: int", "value")]
    [TestCase("var value = 1; while (false) { |value; }", "[variable] value: int", "value")]
    [TestCase("var value = 1; for (var index = 0; index < 1; index = index + 1) { |value; }", "[variable] value: int", "value")]
    [TestCase("try { } catch (|failure) { failure.message; }", "[variable] failure$: error", "failure")]
    [TestCase("try { } catch (failure) { |failure.message; }", "[variable] failure$: error", "failure")]
    [TestCase("try { } catch (failure) { failure.|message; }", "[property] message$: string", "message")]
    [TestCase("var value = 1; try { } finally { |value; }", "[variable] value: int", "value")]
    [TestCase("try { } catch (failure) { throw |failure; }", "[variable] failure$: error", "failure")]
    [TestCase("var value = 1; |value; missing;", "[variable] value: int", "value")]
    [TestCase("var value = 1; (|value);", "[variable] value: int", "value")]
    [TestCase("var |value = missing;", "[variable] value: <unresolved>", "value")]
    [TestCase("var value = |missing;", "[variable] missing: <unresolved>", "missing")]
    [TestCase("var value = |1;", "[literal] 1: int", "1")]
    [TestCase("var value = -|1;", "[literal] -1: int", "-1")]
    [TestCase("var value = |1.5;", "[literal] 1.5: float", "1.5")]
    [TestCase("var value = |\"text\";", "[literal] \"text\": string", "\"text\"")]
    [TestCase("var value = |true;", "[literal] true: bool", "true")]
    [TestCase("var value = |null;", "[literal] null: <null>", "null")]
    [TestCase("var value = |infty;", "[literal] infty: float", "infty")]
    [TestCase("var value = |missing(1);", "[function] missing(...): <unresolved>", "missing")]
    [TestCase("var value = missing(|1);", "[literal] 1: int", "1")]
    [TestCase("var value = { known = 1 }; value.|missing;", "[property] missing: <unresolved>", "missing")]
    [TestCase("var value = @{ }; value.|missing;", "[property] missing: unknown?", "missing")]
    [TestCase("var value: object = { }; var key = \"name\"; value[|key];", "[variable] key: string\n[expression] value[key]: unknown?", "key")]
    [TestCase("var value: object = { }; value[|\"name\"];", "[literal] \"name\": string\n[expression] value[\"name\"]: unknown?", "\"name\"")]
    [TestCase("var value = { known = 1 }; value[|\"missing\"];", "[literal] \"missing\": string\n[expression] value[\"missing\"]: <unresolved>", "\"missing\"")]
    [TestCase("var input = 1; var value = |+input;", "[operator] +(int): int", "+")]
    [TestCase("var input = 1; var value = |-input;", "[operator] -(int): int", "-")]
    [TestCase("var value = |!true;", "[operator] !(bool): bool", "!")]
    [TestCase("var value = |~1;", "[operator] ~(int): int", "~")]
    [TestCase("var value = 1 |+ 2;", "[operator] +(int, int): int", "+")]
    [TestCase("var value = 4 |/ 2;", "[operator] /(int, int): int", "/")]
    [TestCase("var value = true |&& false;", "[operator] &&(bool, bool): bool", "&&")]
    [TestCase("var value = 1.0 |> 2.0;", "[operator] >(float, float): bool", ">")]
    [TestCase("var value = 1 |== 2;", "[operator] ==(int, int): bool", "==")]
    [TestCase("var value = 1 |=== 2;", "[operator] ===(int, int): bool", "===")]
    [TestCase("var value = 1 |& 2;", "[operator] &(int, int): int", "&")]
    [TestCase("var value = 1 |<< 2;", "[operator] <<(int, int): int", "<<")]
    [TestCase("var value = \"text\" |- 1;", "[operator] -(string, int): <unresolved>", "-")]
    [TestCase("var value = missing |+ 1;", "[operator] +(<unresolved>, int): <unresolved>", "+")]
    [TestCase("var source: int? = 1; var value = source |?? 0;", "[operator] ??(int?, int): int", "??")]
    [TestCase("var value = 1 |?? \"fallback\";", "[operator] ??(int, string): <unresolved>", "??")]
    [TestCase("var value = true |? 1 : 2;", "[operator] ?:(bool, int, int): int", "?")]
    [TestCase("var value = true ? 1 |: 2;", "[operator] ?:(bool, int, int): int", ":")]
    [TestCase("func noop(): void { } var value = true |? noop() : 1;", "[operator] ?:(bool, void, int): <unresolved>", "?")]
    [TestCase("var value = (|1)(2);", "[literal] 1: int", "1")]
    [TestCase("var obj = { method = 1 }; var value = obj.|method();", "[property] method: int", "method")]
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
            Assert.That(hover.Span.Start, Is.LessThanOrEqualTo(offset));
            Assert.That(hover.Span.End, Is.GreaterThan(offset));
        }
    }

    [TestCase("|customer", "[variable] customer$: Customer", "customer")]
    [TestCase("|load(customer)", "[function] load(value: Customer): Customer", "load")]
    [TestCase("customer.|name", "[property] name$?: string", "name")]
    [TestCase("customer is |Customer", "[type] Customer { name$?: string }", "Customer")]
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
    [TestCase("var value = 1;|")]
    [TestCase("var value: |Missing;")]
    [TestCase("var value = 1; |(value);")]
    [TestCase("var value = 1 |is int;")]
    [TestCase("var value = { item = 1 } |has \"item\";")]
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
        Assert.That(
            hover!.Content,
            Is.EqualTo("[variable] café: int")
        );
        Assert.That(SourceText.From(source).GetText(hover.Span), Is.EqualTo("café"));
    }

    [Test]
    public void ExposesCategorizedHoverItems()
    {
        const string source =
            "var value: object = { }; var key = \"name\"; value[key];";
        int offset = source.LastIndexOf(
            "key",
            StringComparison.Ordinal
        );
        HoverInfo? hover = MuLangCompiler.GetHover(
            source,
            new EnvironmentBuilder().Build(),
            CompilationMode.Program,
            offset
        );

        Assert.That(hover, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                hover!.Items.Select(static item => item.Category),
                Is.EqualTo(
                    [
                        SemanticHoverCategory.Variable,
                        SemanticHoverCategory.Expression,
                    ]
                )
            );
            Assert.That(
                hover.Items.Select(static item => item.Content),
                Is.EqualTo(
                    [
                        "key: string",
                        "value[key]: unknown?",
                    ]
                )
            );
        }
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
