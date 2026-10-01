using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.StandardLibrary;

/// <summary>Provides the canonical standard-library catalog.</summary>
public static class StandardLibraryCatalog
{
    /// <summary>Gets every canonical symbol in deterministic catalog order.</summary>
    public static IReadOnlyList<IStandardLibrarySymbol> Symbols { get; } =
        Array.AsReadOnly<IStandardLibrarySymbol>(
            [
                .. Types.All,
                .. Globals.All,
                .. Functions.All,
            ]
        );

    /// <summary>Provides canonical structured types.</summary>
    public static class Types
    {
        /// <summary>Gets every canonical structured type in deterministic catalog order.</summary>
        public static IReadOnlyList<StandardLibraryType> All { get; } =
            Array.AsReadOnly<StandardLibraryType>([ ]);

        /// <summary>Gets a structured type by provider identifier.</summary>
        /// <param name="id">The provider identifier.</param>
        /// <param name="type">When this method returns, contains the structured type, if found.</param>
        /// <returns><c>true</c> if a type was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetById(string id, [NotNullWhen(true)] out StandardLibraryType? type)
        {
            type = All.FirstOrDefault(
                item => string.Equals(item.Id, id, StringComparison.Ordinal)
            );
            return type is not null;
        }

        /// <summary>Gets a structured type by language name.</summary>
        /// <param name="name">The language name.</param>
        /// <param name="type">When this method returns, contains the structured type, if found.</param>
        /// <returns><c>true</c> if a type was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetByName(string name, [NotNullWhen(true)] out StandardLibraryType? type)
        {
            type = All.FirstOrDefault(
                item => string.Equals(item.Name, name, StringComparison.Ordinal)
            );
            return type is not null;
        }
    }

    /// <summary>Provides canonical globals.</summary>
    public static class Globals
    {
        /// <summary>Gets the <c>e</c> global.</summary>
        public static StandardLibraryGlobal E { get; } = Create("math.constants", "e", TypeSymbols.Float);

        /// <summary>Gets the <c>pi</c> global.</summary>
        public static StandardLibraryGlobal Pi { get; } = Create("math.constants", "pi", TypeSymbols.Float);

        /// <summary>Gets the <c>tau</c> global.</summary>
        public static StandardLibraryGlobal Tau { get; } = Create("math.constants", "tau", TypeSymbols.Float);

        /// <summary>Gets the <c>minInt</c> global.</summary>
        public static StandardLibraryGlobal MinInt { get; } = Create("math.constants", "minInt", TypeSymbols.Int);

        /// <summary>Gets the <c>maxInt</c> global.</summary>
        public static StandardLibraryGlobal MaxInt { get; } = Create("math.constants", "maxInt", TypeSymbols.Int);

        /// <summary>Gets every canonical global in deterministic catalog order.</summary>
        public static IReadOnlyList<StandardLibraryGlobal> All { get; } =
        [
            E,
            Pi,
            Tau,
            MinInt,
            MaxInt,
        ];

        /// <summary>Gets a global by provider identifier.</summary>
        /// <param name="id">The provider identifier.</param>
        /// <param name="global">When this method returns, contains the global, if found.</param>
        /// <returns><c>true</c> if a global was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetById(string id, [NotNullWhen(true)] out StandardLibraryGlobal? global)
        {
            global = All.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return global is not null;
        }

        /// <summary>Gets a global by language name.</summary>
        /// <param name="name">The language name.</param>
        /// <param name="global">When this method returns, contains the global, if found.</param>
        /// <returns><c>true</c> if a global was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetByName(string name, [NotNullWhen(true)] out StandardLibraryGlobal? global)
        {
            global = All.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            return global is not null;
        }

        private static StandardLibraryGlobal Create(string module, string name, TypeSymbol type)
        {
            return new StandardLibraryGlobal(
                new GlobalSymbol($"mulang.std.{module}.global.{name}", name, type)
            );
        }
    }

    /// <summary>Provides canonical functions.</summary>
    public static class Functions
    {
        /// <summary>Gets the <c>abs</c> function.</summary>
        public static StandardLibraryFunction Abs { get; } = Create(
            "math.basic", "abs", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>sign</c> function.</summary>
        public static StandardLibraryFunction Sign { get; } = Create(
            "math.basic", "sign", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>min</c> function.</summary>
        public static StandardLibraryFunction Min { get; } = Create(
            "math.basic", "min", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("left", TypeSymbols.Number), ("right", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>max</c> function.</summary>
        public static StandardLibraryFunction Max { get; } = Create(
            "math.basic", "max", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("left", TypeSymbols.Number), ("right", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>clamp</c> function.</summary>
        public static StandardLibraryFunction Clamp { get; } = Create(
            "math.basic", "clamp", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number), ("minimum", TypeSymbols.Number), ("maximum", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>floor</c> function.</summary>
        public static StandardLibraryFunction Floor { get; } = Create(
            "math.rounding", "floor", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>ceiling</c> function.</summary>
        public static StandardLibraryFunction Ceiling { get; } = Create(
            "math.rounding", "ceiling", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>truncate</c> function.</summary>
        public static StandardLibraryFunction Truncate { get; } = Create(
            "math.rounding", "truncate", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>round</c> function.</summary>
        public static StandardLibraryFunction Round { get; } = Create(
            "math.rounding", "round", TypeSymbols.Number, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>truncateToInt</c> function.</summary>
        public static StandardLibraryFunction TruncateToInt { get; } = Create(
            "math.rounding", "truncateToInt", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Float)
        );

        /// <summary>Gets the <c>sqrt</c> function.</summary>
        public static StandardLibraryFunction Sqrt { get; } = Create(
            "math.powers", "sqrt", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>pow</c> function.</summary>
        public static StandardLibraryFunction Pow { get; } = Create(
            "math.powers", "pow", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number), ("exponent", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>exp</c> function.</summary>
        public static StandardLibraryFunction Exp { get; } = Create(
            "math.powers", "exp", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>log</c> function.</summary>
        public static StandardLibraryFunction Log { get; } = Create(
            "math.powers", "log", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>log10</c> function.</summary>
        public static StandardLibraryFunction Log10 { get; } = Create(
            "math.powers", "log10", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>sin</c> function.</summary>
        public static StandardLibraryFunction Sin { get; } = Create(
            "math.trigonometry", "sin", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>cos</c> function.</summary>
        public static StandardLibraryFunction Cos { get; } = Create(
            "math.trigonometry", "cos", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>tan</c> function.</summary>
        public static StandardLibraryFunction Tan { get; } = Create(
            "math.trigonometry", "tan", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>asin</c> function.</summary>
        public static StandardLibraryFunction Asin { get; } = Create(
            "math.trigonometry", "asin", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>acos</c> function.</summary>
        public static StandardLibraryFunction Acos { get; } = Create(
            "math.trigonometry", "acos", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>atan</c> function.</summary>
        public static StandardLibraryFunction Atan { get; } = Create(
            "math.trigonometry", "atan", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>atan2</c> function.</summary>
        public static StandardLibraryFunction Atan2 { get; } = Create(
            "math.trigonometry", "atan2", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("y", TypeSymbols.Number), ("x", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>degreesToRadians</c> function.</summary>
        public static StandardLibraryFunction DegreesToRadians { get; } = Create(
            "math.trigonometry", "degreesToRadians", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>radiansToDegrees</c> function.</summary>
        public static StandardLibraryFunction RadiansToDegrees { get; } = Create(
            "math.trigonometry", "radiansToDegrees", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>isFinite</c> function.</summary>
        public static StandardLibraryFunction IsFinite { get; } = Create(
            "math.classification", "isFinite", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>isInfinity</c> function.</summary>
        public static StandardLibraryFunction IsInfinity { get; } = Create(
            "math.classification", "isInfinity", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>isNaN</c> function.</summary>
        public static StandardLibraryFunction IsNaN { get; } = Create(
            "math.classification", "isNaN", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.Number)
        );

        /// <summary>Gets the <c>arrayContains</c> function.</summary>
        public static StandardLibraryFunction ArrayContains { get; } = Create(
            "array", "arrayContains", TypeSymbols.Bool, LanguageVersion.Version1_1, StandardLibraryCapability.Deterministic, ("array", TypeSymbols.ReadOnlyArray(TypeSymbols.Nullable(TypeSymbols.Unknown))), ("value", TypeSymbols.Nullable(TypeSymbols.Unknown))
        );

        /// <summary>Gets the <c>objectKeys</c> function.</summary>
        public static StandardLibraryFunction ObjectKeys { get; } = Create(
            "object", "objectKeys", TypeSymbols.ReadOnlyArray(TypeSymbols.String), LanguageVersion.Version1_1, StandardLibraryCapability.Deterministic, ("obj", TypeSymbols.Object)
        );

        /// <summary>Gets the <c>objectValues</c> function.</summary>
        public static StandardLibraryFunction ObjectValues { get; } = Create(
            "object", "objectValues", TypeSymbols.ReadOnlyArray(TypeSymbols.Nullable(TypeSymbols.Unknown)), LanguageVersion.Version1_1, StandardLibraryCapability.Deterministic, ("obj", TypeSymbols.Object)
        );

        /// <summary>Gets the <c>stringLength</c> function.</summary>
        public static StandardLibraryFunction StringLength { get; } = Create(
            "string.inspection", "stringLength", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>charAt</c> function.</summary>
        public static StandardLibraryFunction CharAt { get; } = Create(
            "string.inspection", "charAt", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("index", TypeSymbols.Int)
        );

        /// <summary>Gets the <c>isEmpty</c> function.</summary>
        public static StandardLibraryFunction IsEmpty { get; } = Create(
            "string.inspection", "isEmpty", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>isWhiteSpace</c> function.</summary>
        public static StandardLibraryFunction IsWhiteSpace { get; } = Create(
            "string.inspection", "isWhiteSpace", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>stringContains</c> function.</summary>
        public static StandardLibraryFunction StringContains { get; } = Create(
            "string.inspection", "stringContains", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("part", TypeSymbols.String)
        );

        /// <summary>Gets the <c>startsWith</c> function.</summary>
        public static StandardLibraryFunction StartsWith { get; } = Create(
            "string.inspection", "startsWith", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("prefix", TypeSymbols.String)
        );

        /// <summary>Gets the <c>endsWith</c> function.</summary>
        public static StandardLibraryFunction EndsWith { get; } = Create(
            "string.inspection", "endsWith", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("suffix", TypeSymbols.String)
        );

        /// <summary>Gets the <c>indexOf</c> function.</summary>
        public static StandardLibraryFunction IndexOf { get; } = Create(
            "string.search", "indexOf", TypeSymbols.Nullable(TypeSymbols.Int), LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("part", TypeSymbols.String)
        );

        /// <summary>Gets the <c>lastIndexOf</c> function.</summary>
        public static StandardLibraryFunction LastIndexOf { get; } = Create(
            "string.search", "lastIndexOf", TypeSymbols.Nullable(TypeSymbols.Int), LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("part", TypeSymbols.String)
        );

        /// <summary>Gets the <c>toLower</c> function.</summary>
        public static StandardLibraryFunction ToLower { get; } = Create(
            "string.transform", "toLower", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>toUpper</c> function.</summary>
        public static StandardLibraryFunction ToUpper { get; } = Create(
            "string.transform", "toUpper", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>trim</c> function.</summary>
        public static StandardLibraryFunction Trim { get; } = Create(
            "string.transform", "trim", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>trimStart</c> function.</summary>
        public static StandardLibraryFunction TrimStart { get; } = Create(
            "string.transform", "trimStart", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>trimEnd</c> function.</summary>
        public static StandardLibraryFunction TrimEnd { get; } = Create(
            "string.transform", "trimEnd", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>repeat</c> function.</summary>
        public static StandardLibraryFunction Repeat { get; } = Create(
            "string.transform", "repeat", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("count", TypeSymbols.Int)
        );

        /// <summary>Gets the <c>reverse</c> function.</summary>
        public static StandardLibraryFunction Reverse { get; } = Create(
            "string.transform", "reverse", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>substring</c> function.</summary>
        public static StandardLibraryFunction Substring { get; } = Create(
            "string.slicing", "substring", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("start", TypeSymbols.Int), ("length", TypeSymbols.Int)
        );

        /// <summary>Gets the <c>remove</c> function.</summary>
        public static StandardLibraryFunction Remove { get; } = Create(
            "string.slicing", "remove", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("start", TypeSymbols.Int), ("length", TypeSymbols.Int)
        );

        /// <summary>Gets the <c>insert</c> function.</summary>
        public static StandardLibraryFunction Insert { get; } = Create(
            "string.slicing", "insert", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("index", TypeSymbols.Int), ("inserted", TypeSymbols.String)
        );

        /// <summary>Gets the <c>replaceFirst</c> function.</summary>
        public static StandardLibraryFunction ReplaceFirst { get; } = Create(
            "string.replacement", "replaceFirst", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("oldValue", TypeSymbols.String), ("newValue", TypeSymbols.String)
        );

        /// <summary>Gets the <c>replaceAll</c> function.</summary>
        public static StandardLibraryFunction ReplaceAll { get; } = Create(
            "string.replacement", "replaceAll", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String), ("oldValue", TypeSymbols.String), ("newValue", TypeSymbols.String)
        );

        /// <summary>Gets the <c>compareOrdinal</c> function.</summary>
        public static StandardLibraryFunction CompareOrdinal { get; } = Create(
            "string.comparison", "compareOrdinal", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("left", TypeSymbols.String), ("right", TypeSymbols.String)
        );

        /// <summary>Gets the <c>compareIgnoreCase</c> function.</summary>
        public static StandardLibraryFunction CompareIgnoreCase { get; } = Create(
            "string.comparison", "compareIgnoreCase", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("left", TypeSymbols.String), ("right", TypeSymbols.String)
        );

        /// <summary>Gets the <c>equalsIgnoreCase</c> function.</summary>
        public static StandardLibraryFunction EqualsIgnoreCase { get; } = Create(
            "string.comparison", "equalsIgnoreCase", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("left", TypeSymbols.String), ("right", TypeSymbols.String)
        );

        /// <summary>Gets the <c>parseInt</c> function.</summary>
        public static StandardLibraryFunction ParseInt { get; } = Create(
            "parsing", "parseInt", TypeSymbols.Nullable(TypeSymbols.Int), LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>parseFloat</c> function.</summary>
        public static StandardLibraryFunction ParseFloat { get; } = Create(
            "parsing", "parseFloat", TypeSymbols.Nullable(TypeSymbols.Float), LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>parseBool</c> function.</summary>
        public static StandardLibraryFunction ParseBool { get; } = Create(
            "parsing", "parseBool", TypeSymbols.Nullable(TypeSymbols.Bool), LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>randomFloat</c> function.</summary>
        public static StandardLibraryFunction RandomFloat { get; } = Create(
            "random", "randomFloat", TypeSymbols.Float, LanguageVersion.Version1, StandardLibraryCapability.Randomness
        );

        /// <summary>Gets the <c>randomInt</c> function.</summary>
        public static StandardLibraryFunction RandomInt { get; } = Create(
            "random", "randomInt", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Randomness, ("minimum", TypeSymbols.Int), ("maximum", TypeSymbols.Int)
        );

        /// <summary>Gets the <c>unixTimeSeconds</c> function.</summary>
        public static StandardLibraryFunction UnixTimeSeconds { get; } = Create(
            "clock", "unixTimeSeconds", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Clock
        );

        /// <summary>Gets the <c>unixTimeMilliseconds</c> function.</summary>
        public static StandardLibraryFunction UnixTimeMilliseconds { get; } = Create(
            "clock", "unixTimeMilliseconds", TypeSymbols.Int, LanguageVersion.Version1, StandardLibraryCapability.Clock
        );

        /// <summary>Gets the <c>newGuid</c> function.</summary>
        public static StandardLibraryFunction NewGuid { get; } = Create(
            "guid", "newGuid", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Randomness
        );

        /// <summary>Gets the <c>newGuidV7</c> function.</summary>
        public static StandardLibraryFunction NewGuidV7 { get; } = Create(
            "guid", "newGuidV7", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.RandomnessAndClock
        );

        /// <summary>Gets the <c>isGuid</c> function.</summary>
        public static StandardLibraryFunction IsGuid { get; } = Create(
            "guid", "isGuid", TypeSymbols.Bool, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>base64Encode</c> function.</summary>
        public static StandardLibraryFunction Base64Encode { get; } = Create(
            "text.encoding", "base64Encode", TypeSymbols.String, LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets the <c>base64Decode</c> function.</summary>
        public static StandardLibraryFunction Base64Decode { get; } = Create(
            "text.encoding", "base64Decode", TypeSymbols.Nullable(TypeSymbols.String), LanguageVersion.Version1, StandardLibraryCapability.Deterministic, ("value", TypeSymbols.String)
        );

        /// <summary>Gets every canonical function in deterministic catalog order.</summary>
        public static IReadOnlyList<StandardLibraryFunction> All { get; } =
            Array.AsReadOnly<StandardLibraryFunction>(
                [
                    Abs,
                    Sign,
                    Min,
                    Max,
                    Clamp,
                    Floor,
                    Ceiling,
                    Truncate,
                    Round,
                    TruncateToInt,
                    Sqrt,
                    Pow,
                    Exp,
                    Log,
                    Log10,
                    Sin,
                    Cos,
                    Tan,
                    Asin,
                    Acos,
                    Atan,
                    Atan2,
                    DegreesToRadians,
                    RadiansToDegrees,
                    IsFinite,
                    IsInfinity,
                    IsNaN,
                    ArrayContains,
                    ObjectKeys,
                    ObjectValues,
                    StringLength,
                    CharAt,
                    IsEmpty,
                    IsWhiteSpace,
                    StringContains,
                    StartsWith,
                    EndsWith,
                    IndexOf,
                    LastIndexOf,
                    ToLower,
                    ToUpper,
                    Trim,
                    TrimStart,
                    TrimEnd,
                    Repeat,
                    Reverse,
                    Substring,
                    Remove,
                    Insert,
                    ReplaceFirst,
                    ReplaceAll,
                    CompareOrdinal,
                    CompareIgnoreCase,
                    EqualsIgnoreCase,
                    ParseInt,
                    ParseFloat,
                    ParseBool,
                    RandomFloat,
                    RandomInt,
                    UnixTimeSeconds,
                    UnixTimeMilliseconds,
                    NewGuid,
                    NewGuidV7,
                    IsGuid,
                    Base64Encode,
                    Base64Decode,
                ]
            );

        /// <summary>Gets a function by provider identifier.</summary>
        /// <param name="id">The provider identifier.</param>
        /// <param name="function">When this method returns, contains the function, if found.</param>
        /// <returns><c>true</c> if a function was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetById(string id, [NotNullWhen(true)] out StandardLibraryFunction? function)
        {
            function = All.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return function is not null;
        }

        /// <summary>Gets a function by language name.</summary>
        /// <param name="name">The language name.</param>
        /// <param name="function">When this method returns, contains the function, if found.</param>
        /// <returns><c>true</c> if a function was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetByName(string name, [NotNullWhen(true)] out StandardLibraryFunction? function)
        {
            function = All.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            return function is not null;
        }

        private static StandardLibraryFunction Create(
            string module,
            string name,
            TypeSymbol returnType,
            LanguageVersion minimumLanguageVersion,
            StandardLibraryCapability capability,
            params (string Name, TypeSymbol Type)[] parameters
        )
        {
            return new StandardLibraryFunction(
                new FunctionSymbol(
                    $"mulang.std.{module}.function.{name}",
                    name,
                    parameters.Select(static parameter => new ParameterSymbol(parameter.Name, parameter.Type)),
                    returnType
                ),
                minimumLanguageVersion,
                capability
            );
        }
    }

    /// <summary>Provides canonical modules.</summary>
    public static class Modules
    {
        /// <summary>Gets the Math.Constants module.</summary>
        public static StandardLibraryModule MathConstants { get; } = new (
            "mulang.std.math.constants",
            "Math.Constants",
            [
                Globals.E,
                Globals.Pi,
                Globals.Tau,
                Globals.MinInt,
                Globals.MaxInt,
            ]
        );

        /// <summary>Gets the Math.Basic module.</summary>
        public static StandardLibraryModule MathBasic { get; } = new (
            "mulang.std.math.basic",
            "Math.Basic",
            [
                Functions.Abs,
                Functions.Sign,
                Functions.Min,
                Functions.Max,
                Functions.Clamp,
            ]
        );

        /// <summary>Gets the Math.Rounding module.</summary>
        public static StandardLibraryModule MathRounding { get; } = new (
            "mulang.std.math.rounding",
            "Math.Rounding",
            [
                Functions.Floor,
                Functions.Ceiling,
                Functions.Truncate,
                Functions.Round,
                Functions.TruncateToInt,
            ]
        );

        /// <summary>Gets the Math.Powers module.</summary>
        public static StandardLibraryModule MathPowers { get; } = new (
            "mulang.std.math.powers",
            "Math.Powers",
            [
                Functions.Sqrt,
                Functions.Pow,
                Functions.Exp,
                Functions.Log,
                Functions.Log10,
            ]
        );

        /// <summary>Gets the Math.Trigonometry module.</summary>
        public static StandardLibraryModule MathTrigonometry { get; } = new (
            "mulang.std.math.trigonometry",
            "Math.Trigonometry",
            [
                Functions.Sin,
                Functions.Cos,
                Functions.Tan,
                Functions.Asin,
                Functions.Acos,
                Functions.Atan,
                Functions.Atan2,
                Functions.DegreesToRadians,
                Functions.RadiansToDegrees,
            ]
        );

        /// <summary>Gets the Math.Classification module.</summary>
        public static StandardLibraryModule MathClassification { get; } = new (
            "mulang.std.math.classification",
            "Math.Classification",
            [
                Functions.IsFinite,
                Functions.IsInfinity,
                Functions.IsNaN,
            ]
        );

        /// <summary>Gets the Array module.</summary>
        public static StandardLibraryModule Array { get; } = new (
            "mulang.std.array",
            "Array",
            [
                Functions.ArrayContains,
            ]
        );

        /// <summary>Gets the Object module.</summary>
        public static StandardLibraryModule Object { get; } = new (
            "mulang.std.object",
            "Object",
            [
                Functions.ObjectKeys,
                Functions.ObjectValues,
            ]
        );

        /// <summary>Gets the String.Inspection module.</summary>
        public static StandardLibraryModule StringInspection { get; } = new (
            "mulang.std.string.inspection",
            "String.Inspection",
            [
                Functions.StringLength,
                Functions.CharAt,
                Functions.IsEmpty,
                Functions.IsWhiteSpace,
                Functions.StringContains,
                Functions.StartsWith,
                Functions.EndsWith,
            ]
        );

        /// <summary>Gets the String.Search module.</summary>
        public static StandardLibraryModule StringSearch { get; } = new (
            "mulang.std.string.search",
            "String.Search",
            [
                Functions.IndexOf,
                Functions.LastIndexOf,
            ]
        );

        /// <summary>Gets the String.Transform module.</summary>
        public static StandardLibraryModule StringTransform { get; } = new (
            "mulang.std.string.transform",
            "String.Transform",
            [
                Functions.ToLower,
                Functions.ToUpper,
                Functions.Trim,
                Functions.TrimStart,
                Functions.TrimEnd,
                Functions.Repeat,
                Functions.Reverse,
            ]
        );

        /// <summary>Gets the String.Slicing module.</summary>
        public static StandardLibraryModule StringSlicing { get; } = new (
            "mulang.std.string.slicing",
            "String.Slicing",
            [
                Functions.Substring,
                Functions.Remove,
                Functions.Insert,
            ]
        );

        /// <summary>Gets the String.Replacement module.</summary>
        public static StandardLibraryModule StringReplacement { get; } = new (
            "mulang.std.string.replacement",
            "String.Replacement",
            [
                Functions.ReplaceFirst,
                Functions.ReplaceAll,
            ]
        );

        /// <summary>Gets the String.Comparison module.</summary>
        public static StandardLibraryModule StringComparison { get; } = new (
            "mulang.std.string.comparison",
            "String.Comparison",
            [
                Functions.CompareOrdinal,
                Functions.CompareIgnoreCase,
                Functions.EqualsIgnoreCase,
            ]
        );

        /// <summary>Gets the Parsing module.</summary>
        public static StandardLibraryModule Parsing { get; } = new (
            "mulang.std.parsing",
            "Parsing",
            [
                Functions.ParseInt,
                Functions.ParseFloat,
                Functions.ParseBool,
            ]
        );

        /// <summary>Gets the Random module.</summary>
        public static StandardLibraryModule Random { get; } = new (
            "mulang.std.random",
            "Random",
            [
                Functions.RandomFloat,
                Functions.RandomInt,
            ]
        );

        /// <summary>Gets the Clock module.</summary>
        public static StandardLibraryModule Clock { get; } = new (
            "mulang.std.clock",
            "Clock",
            [
                Functions.UnixTimeSeconds,
                Functions.UnixTimeMilliseconds,
            ]
        );

        /// <summary>Gets the Guid module.</summary>
        public static StandardLibraryModule Guid { get; } = new (
            "mulang.std.guid",
            "Guid",
            [
                Functions.NewGuid,
                Functions.NewGuidV7,
                Functions.IsGuid,
            ]
        );

        /// <summary>Gets the Text.Encoding module.</summary>
        public static StandardLibraryModule TextEncoding { get; } = new (
            "mulang.std.text.encoding",
            "Text.Encoding",
            [
                Functions.Base64Encode,
                Functions.Base64Decode,
            ]
        );

        /// <summary>Gets every canonical module in deterministic catalog order.</summary>
        public static IReadOnlyList<StandardLibraryModule> All { get; } =
            System.Array.AsReadOnly<StandardLibraryModule>(
                [
                    MathConstants,
                    MathBasic,
                    MathRounding,
                    MathPowers,
                    MathTrigonometry,
                    MathClassification,
                    Array,
                    Object,
                    StringInspection,
                    StringSearch,
                    StringTransform,
                    StringSlicing,
                    StringReplacement,
                    StringComparison,
                    Parsing,
                    Random,
                    Clock,
                    Guid,
                    TextEncoding,
                ]
            );

        /// <summary>Gets a module by identifier.</summary>
        /// <param name="id">The module identifier.</param>
        /// <param name="module">When this method returns, contains the module, if found.</param>
        /// <returns><c>true</c> if a module was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetById(string id, [NotNullWhen(true)] out StandardLibraryModule? module)
        {
            module = All.FirstOrDefault(
                item => string.Equals(item.Id, id, System.StringComparison.Ordinal)
            );
            return module is not null;
        }

        /// <summary>Gets a module by display name.</summary>
        /// <param name="name">The display name.</param>
        /// <param name="module">When this method returns, contains the module, if found.</param>
        /// <returns><c>true</c> if a module was found; otherwise, <c>false</c>.</returns>
        public static bool TryGetByName(string name, [NotNullWhen(true)] out StandardLibraryModule? module)
        {
            module = All.FirstOrDefault(
                item => string.Equals(
                    item.DisplayName,
                    name,
                    System.StringComparison.Ordinal
                )
            );
            return module is not null;
        }
    }
}
