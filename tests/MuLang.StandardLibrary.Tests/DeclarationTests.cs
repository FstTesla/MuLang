using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Collections;

namespace MuLang.StandardLibrary.Tests;

public sealed class DeclarationTests
{
    [Test]
    public void PreservesGlobalAndFunctionDeclarations()
    {
        Assert.That(
            StandardLibraryCatalog.Globals.All.Select(
                static global => $"{global.Name}: {global.Declaration.Type.DisplayName}"
            ),
            Is.EqualTo(
                [
                    "e: float",
                    "pi: float",
                    "tau: float",
                    "minInt: int",
                    "maxInt: int",
                ]
            )
        );
        Assert.That(
            StandardLibraryCatalog.Functions.All.Select(static function => Format(function.Declaration)),
            Is.EqualTo(ExpectedFunctionSignatures)
        );
    }

    [Test]
    public void PreservesProviderIdentifiers()
    {
        foreach (StandardLibraryModule module in StandardLibraryCatalog.Modules.All)
        {
            foreach (IStandardLibrarySymbol symbol in module.Symbols)
            {
                string kind = symbol switch
                {
                    StandardLibraryType => "type",
                    StandardLibraryGlobal => "global",
                    StandardLibraryFunction => "function",
                    _ => throw new InvalidOperationException(),
                };

                Assert.That(symbol.Id, Is.EqualTo($"{module.Id}.{kind}.{symbol.Name}"));
            }
        }
    }

    [Test]
    public void SymbolWrappersExposeTypedDeclarationsAndReadOnlyDependencies()
    {
        StandardLibraryGlobal dependency = new (
            new GlobalSymbol("mulang.std.test.global.value", "value", TypeSymbols.Int)
        );
        List<IStandardLibrarySymbol> dependencies = [ dependency ];
        StandardLibraryFunction function = new (
            new FunctionSymbol(
                "mulang.std.test.function.read",
                "read",
                [ ],
                TypeSymbols.Int
            ),
            LanguageVersion.Version1_1,
            StandardLibraryCapability.Clock,
            dependencies
        );

        dependencies.Clear();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(function.Id, Is.EqualTo(function.Declaration.Id));
            Assert.That(function.Name, Is.EqualTo(function.Declaration.Name));
            Assert.That(function.Dependencies, Is.EqualTo([ dependency ]));
            Assert.That(function.Dependencies, Is.AssignableTo<IList>());
            Assert.That(
                () => ((IList)function.Dependencies).Add(dependency),
                Throws.InstanceOf<NotSupportedException>()
            );
        }
    }

    [Test]
    public void ModuleRejectsInvalidShape()
    {
        StandardLibraryGlobal symbol = new (
            new GlobalSymbol("mulang.std.other.global.value", "value", TypeSymbols.Int)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                static () => new StandardLibraryModule("mulang.std.test", "Test", [ ]),
                Throws.ArgumentException
            );
            Assert.That(
                () => new StandardLibraryModule("mulang.std.test", "Test", [ symbol ]),
                Throws.ArgumentException.With.Message.Contains(symbol.Id)
            );
            Assert.That(
                () => new StandardLibraryModule(
                    "mulang.std.other",
                    "Other",
                    [ symbol, symbol ]
                ),
                Throws.ArgumentException.With.Message.Contains(symbol.Id)
            );
        }
    }

    private static string Format(FunctionSymbol function)
    {
        string parameters = string.Join(
            ", ",
            function.Parameters.Select(
                static parameter => $"{parameter.Name}: {parameter.Type.DisplayName}"
            )
        );

        return $"{function.Name}({parameters}): {function.ReturnType.DisplayName}";
    }

    private static readonly string[] ExpectedFunctionSignatures =
    [
        "abs(value: number): number",
        "sign(value: number): int",
        "min(left: number, right: number): number",
        "max(left: number, right: number): number",
        "clamp(value: number, minimum: number, maximum: number): number",
        "floor(value: number): number",
        "ceiling(value: number): number",
        "truncate(value: number): number",
        "round(value: number): number",
        "truncateToInt(value: float): int",
        "sqrt(value: number): float",
        "pow(value: number, exponent: number): float",
        "exp(value: number): float",
        "log(value: number): float",
        "log10(value: number): float",
        "sin(value: number): float",
        "cos(value: number): float",
        "tan(value: number): float",
        "asin(value: number): float",
        "acos(value: number): float",
        "atan(value: number): float",
        "atan2(y: number, x: number): float",
        "degreesToRadians(value: number): float",
        "radiansToDegrees(value: number): float",
        "isFinite(value: number): bool",
        "isInfinity(value: number): bool",
        "isNaN(value: number): bool",
        "arrayContains(array: unknown?[]$, value: unknown?): bool",
        "objectKeys(obj: object): string[]$",
        "objectValues(obj: object): unknown?[]$",
        "stringLength(value: string): int",
        "charAt(value: string, index: int): int",
        "isEmpty(value: string): bool",
        "isWhiteSpace(value: string): bool",
        "stringContains(value: string, part: string): bool",
        "startsWith(value: string, prefix: string): bool",
        "endsWith(value: string, suffix: string): bool",
        "indexOf(value: string, part: string): int?",
        "lastIndexOf(value: string, part: string): int?",
        "toLower(value: string): string",
        "toUpper(value: string): string",
        "trim(value: string): string",
        "trimStart(value: string): string",
        "trimEnd(value: string): string",
        "repeat(value: string, count: int): string",
        "reverse(value: string): string",
        "substring(value: string, start: int, length: int): string",
        "remove(value: string, start: int, length: int): string",
        "insert(value: string, index: int, inserted: string): string",
        "replaceFirst(value: string, oldValue: string, newValue: string): string",
        "replaceAll(value: string, oldValue: string, newValue: string): string",
        "compareOrdinal(left: string, right: string): int",
        "compareIgnoreCase(left: string, right: string): int",
        "equalsIgnoreCase(left: string, right: string): bool",
        "parseInt(value: string): int?",
        "parseFloat(value: string): float?",
        "parseBool(value: string): bool?",
        "randomFloat(): float",
        "randomInt(minimum: int, maximum: int): int",
        "unixTimeSeconds(): int",
        "unixTimeMilliseconds(): int",
        "newGuid(): string",
        "newGuidV7(): string",
        "isGuid(value: string): bool",
        "base64Encode(value: string): string",
        "base64Decode(value: string): string?",
    ];
}
