using MuLang.Core;
using MuLang.Core.Symbols;
using MuLang.Core.Types;

namespace MuLang.StandardLibrary;

/// <summary>Provides the canonical standard-library module catalog.</summary>
public static class StandardLibraryCatalog
{
    /// <summary>Gets the Math.Constants module.</summary>
    public static StandardLibraryModule MathConstants { get; } = CreateModule(
        "math.constants",
        [
            ("e", TypeSymbols.Float),
            ("pi", TypeSymbols.Float),
            ("tau", TypeSymbols.Float),
            ("minInt", TypeSymbols.Int),
            ("maxInt", TypeSymbols.Int),
        ],
        [ ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Math.Basic module.</summary>
    public static StandardLibraryModule MathBasic { get; } = CreateModule(
        "math.basic",
        [ ],
        [
            Function("abs", TypeSymbols.Number, ("value", TypeSymbols.Number)),
            Function("sign", TypeSymbols.Int, ("value", TypeSymbols.Number)),
            Function("min", TypeSymbols.Number, ("left", TypeSymbols.Number), ("right", TypeSymbols.Number)),
            Function("max", TypeSymbols.Number, ("left", TypeSymbols.Number), ("right", TypeSymbols.Number)),
            Function(
                "clamp",
                TypeSymbols.Number,
                ("value", TypeSymbols.Number),
                ("minimum", TypeSymbols.Number),
                ("maximum", TypeSymbols.Number)
            ),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Math.Rounding module.</summary>
    public static StandardLibraryModule MathRounding { get; } = CreateModule(
        "math.rounding",
        [ ],
        [
            Function("floor", TypeSymbols.Number, ("value", TypeSymbols.Number)),
            Function("ceiling", TypeSymbols.Number, ("value", TypeSymbols.Number)),
            Function("truncate", TypeSymbols.Number, ("value", TypeSymbols.Number)),
            Function("round", TypeSymbols.Number, ("value", TypeSymbols.Number)),
            Function("truncateToInt", TypeSymbols.Int, ("value", TypeSymbols.Float)),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Math.Powers module.</summary>
    public static StandardLibraryModule MathPowers { get; } = CreateModule(
        "math.powers",
        [ ],
        [
            Function("sqrt", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("pow", TypeSymbols.Float, ("value", TypeSymbols.Number), ("exponent", TypeSymbols.Number)),
            Function("exp", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("log", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("log10", TypeSymbols.Float, ("value", TypeSymbols.Number)),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Math.Trigonometry module.</summary>
    public static StandardLibraryModule MathTrigonometry { get; } = CreateModule(
        "math.trigonometry",
        [ ],
        [
            Function("sin", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("cos", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("tan", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("asin", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("acos", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("atan", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("atan2", TypeSymbols.Float, ("y", TypeSymbols.Number), ("x", TypeSymbols.Number)),
            Function("degreesToRadians", TypeSymbols.Float, ("value", TypeSymbols.Number)),
            Function("radiansToDegrees", TypeSymbols.Float, ("value", TypeSymbols.Number)),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Math.Classification module.</summary>
    public static StandardLibraryModule MathClassification { get; } = CreateModule(
        "math.classification",
        [ ],
        [
            Function("isFinite", TypeSymbols.Bool, ("value", TypeSymbols.Number)),
            Function("isInfinity", TypeSymbols.Bool, ("value", TypeSymbols.Number)),
            Function("isNaN", TypeSymbols.Bool, ("value", TypeSymbols.Number)),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Array module.</summary>
    public static StandardLibraryModule Array { get; } = CreateModule(
        "array",
        [ ],
        [
            Function(
                "arrayContains",
                TypeSymbols.Bool,
                ("array", TypeSymbols.ReadOnlyArray(TypeSymbols.Nullable(TypeSymbols.Unknown))),
                ("value", TypeSymbols.Nullable(TypeSymbols.Unknown))
            ),
        ],
        LanguageVersion.Version1_1
    );

    /// <summary>Gets the Object module.</summary>
    public static StandardLibraryModule Object { get; } = CreateModule(
        "object",
        [ ],
        [
            Function("objectKeys", TypeSymbols.ReadOnlyArray(TypeSymbols.String), ("obj", TypeSymbols.Object)),
            Function(
                "objectValues",
                TypeSymbols.ReadOnlyArray(TypeSymbols.Nullable(TypeSymbols.Unknown)),
                ("obj", TypeSymbols.Object)
            ),
        ],
        LanguageVersion.Version1_1
    );

    /// <summary>Gets the String.Inspection module.</summary>
    public static StandardLibraryModule StringInspection { get; } = CreateModule(
        "string.inspection",
        [ ],
        [
            Function("stringLength", TypeSymbols.Int, ("value", TypeSymbols.String)),
            Function("charAt", TypeSymbols.Int, ("value", TypeSymbols.String), ("index", TypeSymbols.Int)),
            Function("isEmpty", TypeSymbols.Bool, ("value", TypeSymbols.String)),
            Function("isWhiteSpace", TypeSymbols.Bool, ("value", TypeSymbols.String)),
            Function(
                "stringContains",
                TypeSymbols.Bool,
                ("value", TypeSymbols.String),
                ("part", TypeSymbols.String)
            ),
            Function(
                "startsWith",
                TypeSymbols.Bool,
                ("value", TypeSymbols.String),
                ("prefix", TypeSymbols.String)
            ),
            Function(
                "endsWith",
                TypeSymbols.Bool,
                ("value", TypeSymbols.String),
                ("suffix", TypeSymbols.String)
            ),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the String.Search module.</summary>
    public static StandardLibraryModule StringSearch { get; } = CreateModule(
        "string.search",
        [ ],
        [
            Function(
                "indexOf",
                TypeSymbols.Nullable(TypeSymbols.Int),
                ("value", TypeSymbols.String),
                ("part", TypeSymbols.String)
            ),
            Function(
                "lastIndexOf",
                TypeSymbols.Nullable(TypeSymbols.Int),
                ("value", TypeSymbols.String),
                ("part", TypeSymbols.String)
            ),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the String.Transform module.</summary>
    public static StandardLibraryModule StringTransform { get; } = CreateModule(
        "string.transform",
        [ ],
        [
            Function("toLower", TypeSymbols.String, ("value", TypeSymbols.String)),
            Function("toUpper", TypeSymbols.String, ("value", TypeSymbols.String)),
            Function("trim", TypeSymbols.String, ("value", TypeSymbols.String)),
            Function("trimStart", TypeSymbols.String, ("value", TypeSymbols.String)),
            Function("trimEnd", TypeSymbols.String, ("value", TypeSymbols.String)),
            Function(
                "repeat",
                TypeSymbols.String,
                ("value", TypeSymbols.String),
                ("count", TypeSymbols.Int)
            ),
            Function("reverse", TypeSymbols.String, ("value", TypeSymbols.String)),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the String.Slicing module.</summary>
    public static StandardLibraryModule StringSlicing { get; } = CreateModule(
        "string.slicing",
        [ ],
        [
            Function(
                "substring",
                TypeSymbols.String,
                ("value", TypeSymbols.String),
                ("start", TypeSymbols.Int),
                ("length", TypeSymbols.Int)
            ),
            Function(
                "remove",
                TypeSymbols.String,
                ("value", TypeSymbols.String),
                ("start", TypeSymbols.Int),
                ("length", TypeSymbols.Int)
            ),
            Function(
                "insert",
                TypeSymbols.String,
                ("value", TypeSymbols.String),
                ("index", TypeSymbols.Int),
                ("inserted", TypeSymbols.String)
            ),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the String.Replacement module.</summary>
    public static StandardLibraryModule StringReplacement { get; } = CreateModule(
        "string.replacement",
        [ ],
        [
            Function(
                "replaceFirst",
                TypeSymbols.String,
                ("value", TypeSymbols.String),
                ("oldValue", TypeSymbols.String),
                ("newValue", TypeSymbols.String)
            ),
            Function(
                "replaceAll",
                TypeSymbols.String,
                ("value", TypeSymbols.String),
                ("oldValue", TypeSymbols.String),
                ("newValue", TypeSymbols.String)
            ),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the String.Comparison module.</summary>
    public static StandardLibraryModule StringComparison { get; } = CreateModule(
        "string.comparison",
        [ ],
        [
            Function(
                "compareOrdinal",
                TypeSymbols.Int,
                ("left", TypeSymbols.String),
                ("right", TypeSymbols.String)
            ),
            Function(
                "compareIgnoreCase",
                TypeSymbols.Int,
                ("left", TypeSymbols.String),
                ("right", TypeSymbols.String)
            ),
            Function(
                "equalsIgnoreCase",
                TypeSymbols.Bool,
                ("left", TypeSymbols.String),
                ("right", TypeSymbols.String)
            ),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Parsing module.</summary>
    public static StandardLibraryModule Parsing { get; } = CreateModule(
        "parsing",
        [ ],
        [
            Function("parseInt", TypeSymbols.Nullable(TypeSymbols.Int), ("value", TypeSymbols.String)),
            Function("parseFloat", TypeSymbols.Nullable(TypeSymbols.Float), ("value", TypeSymbols.String)),
            Function("parseBool", TypeSymbols.Nullable(TypeSymbols.Bool), ("value", TypeSymbols.String)),
        ],
        LanguageVersion.Version1
    );

    /// <summary>Gets the Random module.</summary>
    public static StandardLibraryModule Random { get; } = CreateModule(
        "random",
        [ ],
        [
            Function("randomFloat", TypeSymbols.Float),
            Function(
                "randomInt",
                TypeSymbols.Int,
                ("minimum", TypeSymbols.Int),
                ("maximum", TypeSymbols.Int)
            ),
        ],
        LanguageVersion.Version1,
        StandardLibraryCapability.Randomness
    );

    /// <summary>Gets the Clock module.</summary>
    public static StandardLibraryModule Clock { get; } = CreateModule(
        "clock",
        [ ],
        [
            Function("unixTimeSeconds", TypeSymbols.Int),
            Function("unixTimeMilliseconds", TypeSymbols.Int),
        ],
        LanguageVersion.Version1,
        StandardLibraryCapability.Clock
    );

    /// <summary>Gets the Guid module.</summary>
    public static StandardLibraryModule Guid { get; } = CreateModule(
        "guid",
        [ ],
        [
            Function("newGuid", TypeSymbols.String),
            Function("newGuidV7", TypeSymbols.String),
            Function("isGuid", TypeSymbols.Bool, ("value", TypeSymbols.String)),
        ],
        LanguageVersion.Version1,
        StandardLibraryCapability.RandomnessAndClock
    );

    /// <summary>Gets the Text.Encoding module.</summary>
    public static StandardLibraryModule TextEncoding { get; } = CreateModule(
        "text.encoding",
        [ ],
        [
            Function("base64Encode", TypeSymbols.String, ("value", TypeSymbols.String)),
            Function(
                "base64Decode",
                TypeSymbols.Nullable(TypeSymbols.String),
                ("value", TypeSymbols.String)
            ),
        ],
        LanguageVersion.Version1
    );

    private static StandardLibraryModule CreateModule(
        string name,
        IReadOnlyList<(string Name, TypeSymbol Type)> globals,
        IReadOnlyList<FunctionDeclaration> functions,
        LanguageVersion minimumLanguageVersion,
        StandardLibraryCapability capability = StandardLibraryCapability.Deterministic
    )
    {
        string moduleId = $"mulang.std.{name}";
        GlobalSymbol[] globalSymbols =
        [
            .. globals.Select(
                global => new GlobalSymbol(
                    $"{moduleId}.global.{global.Name}",
                    global.Name,
                    global.Type
                )
            ),
        ];
        FunctionSymbol[] functionSymbols =
        [
            .. functions.Select(
                function => new FunctionSymbol(
                    $"{moduleId}.function.{function.Name}",
                    function.Name,
                    function.Parameters.Select(
                        static parameter => new ParameterSymbol(parameter.Name, parameter.Type)
                    ),
                    function.ReturnType
                )
            ),
        ];

        return new StandardLibraryModule(
            moduleId,
            string.Join(
                '.',
                name.Split('.').Select(static part => $"{char.ToUpperInvariant(part[0])}{part[1..]}")
            ),
            minimumLanguageVersion,
            capability,
            [ ],
            globalSymbols,
            functionSymbols
        );
    }

    /// <summary>Gets every canonical module in deterministic catalog order.</summary>
    public static IReadOnlyList<StandardLibraryModule> All { get; } =
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
    ];

    private static FunctionDeclaration Function(
        string name,
        TypeSymbol returnType,
        params (string Name, TypeSymbol Type)[] parameters
    )
    {
        return new FunctionDeclaration(name, returnType, parameters);
    }

    private sealed record FunctionDeclaration(
        string Name,
        TypeSymbol ReturnType,
        IReadOnlyList<(string Name, TypeSymbol Type)> Parameters
    );
}
