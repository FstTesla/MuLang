using MuLang.Core.Symbols;

namespace MuLang.StandardLibrary.Tests;

public sealed class DeclarationTests
{
    [Test]
    public void DeclaresExactGlobals()
    {
        Assert.That(
            StandardLibraryCatalog.MathConstants.Globals.Select(
                static global => $"{global.Name}: {global.Type.DisplayName}"
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
    }

    [Test]
    public void DeclaresExactFunctionSignatures()
    {
        string[] expected =
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

        Assert.That(
            StandardLibraryCatalog.All
                .SelectMany(static module => module.Functions)
                .Select(Format),
            Is.EqualTo(expected)
        );
    }

    [Test]
    public void UsesStableDeclarationIdentifiers()
    {
        foreach (StandardLibraryModule module in StandardLibraryCatalog.All)
        {
            foreach (GlobalSymbol global in module.Globals)
            {
                Assert.That(
                    global.Id,
                    Is.EqualTo($"{module.Id}.global.{global.Name}")
                );
            }

            foreach (FunctionSymbol function in module.Functions)
            {
                Assert.That(
                    function.Id,
                    Is.EqualTo($"{module.Id}.function.{function.Name}")
                );
            }
        }
    }

    [Test]
    public void PreservesPlanDeclarationOrder()
    {
        Assert.That(
            StandardLibraryCatalog.MathBasic.Functions.Select(static function => function.Name),
            Is.EqualTo([ "abs", "sign", "min", "max", "clamp" ])
        );
        Assert.That(
            StandardLibraryCatalog.StringInspection.Functions.Select(static function => function.Name),
            Is.EqualTo(
                [
                    "stringLength",
                    "charAt",
                    "isEmpty",
                    "isWhiteSpace",
                    "stringContains",
                    "startsWith",
                    "endsWith",
                ]
            )
        );
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
}
