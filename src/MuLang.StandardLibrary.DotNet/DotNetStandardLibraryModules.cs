using MuLang.Core.Symbols;
using MuLang.Exporters.DotNet;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Provides canonical .NET bindings for standard-library modules.</summary>
public static class DotNetStandardLibraryModules
{
    private const string ConversionError = "mulang.std.conversion";
    private const string InvalidArgumentError = "mulang.std.invalid_argument";
    private const string OverflowError = "mulang.std.overflow";
    private const string RangeError = "mulang.std.range";
    private const string ResourceError = "mulang.std.resource";
    private static readonly UTF8Encoding StrictUtf8 = new (false, true);

    /// <summary>Gets the Math.Constants binding.</summary>
    public static DotNetStandardLibraryModuleBinding MathConstants { get; } = CreateBinding(
        StandardLibraryCatalog.MathConstants,
        [
            Global(StandardLibraryCatalog.MathConstants, "e", Math.E),
            Global(StandardLibraryCatalog.MathConstants, "pi", Math.PI),
            Global(StandardLibraryCatalog.MathConstants, "tau", Math.Tau),
            Global(StandardLibraryCatalog.MathConstants, "minInt", long.MinValue),
            Global(StandardLibraryCatalog.MathConstants, "maxInt", long.MaxValue),
        ],
        [ ]
    );

    /// <summary>Gets the Math.Basic binding.</summary>
    public static DotNetStandardLibraryModuleBinding MathBasic { get; } = CreateBinding(
        StandardLibraryCatalog.MathBasic,
        [ ],
        [
            Function(StandardLibraryCatalog.MathBasic, "abs", Abs),
            Function(StandardLibraryCatalog.MathBasic, "sign", Sign),
            Function(StandardLibraryCatalog.MathBasic, "min", Min),
            Function(StandardLibraryCatalog.MathBasic, "max", Max),
            Function(StandardLibraryCatalog.MathBasic, "clamp", Clamp),
        ]
    );

    /// <summary>Gets the Math.Rounding binding.</summary>
    public static DotNetStandardLibraryModuleBinding MathRounding { get; } = CreateBinding(
        StandardLibraryCatalog.MathRounding,
        [ ],
        [
            Function(StandardLibraryCatalog.MathRounding, "floor", Floor),
            Function(StandardLibraryCatalog.MathRounding, "ceiling", Ceiling),
            Function(StandardLibraryCatalog.MathRounding, "truncate", Truncate),
            Function(StandardLibraryCatalog.MathRounding, "round", Round),
            Function(StandardLibraryCatalog.MathRounding, "truncateToInt", TruncateToInt),
        ]
    );

    /// <summary>Gets the Math.Powers binding.</summary>
    public static DotNetStandardLibraryModuleBinding MathPowers { get; } = CreateBinding(
        StandardLibraryCatalog.MathPowers,
        [ ],
        [
            Function(StandardLibraryCatalog.MathPowers, "sqrt", static (_, arguments) => Math.Sqrt(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathPowers, "pow", static (_, arguments) => Math.Pow(Number(arguments[0]), Number(arguments[1]))),
            Function(StandardLibraryCatalog.MathPowers, "exp", static (_, arguments) => Math.Exp(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathPowers, "log", static (_, arguments) => Math.Log(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathPowers, "log10", static (_, arguments) => Math.Log10(Number(arguments[0]))),
        ]
    );

    /// <summary>Gets the Math.Trigonometry binding.</summary>
    public static DotNetStandardLibraryModuleBinding MathTrigonometry { get; } = CreateBinding(
        StandardLibraryCatalog.MathTrigonometry,
        [ ],
        [
            Function(StandardLibraryCatalog.MathTrigonometry, "sin", static (_, arguments) => Math.Sin(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathTrigonometry, "cos", static (_, arguments) => Math.Cos(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathTrigonometry, "tan", static (_, arguments) => Math.Tan(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathTrigonometry, "asin", static (_, arguments) => Math.Asin(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathTrigonometry, "acos", static (_, arguments) => Math.Acos(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathTrigonometry, "atan", static (_, arguments) => Math.Atan(Number(arguments[0]))),
            Function(StandardLibraryCatalog.MathTrigonometry, "atan2", static (_, arguments) => Math.Atan2(Number(arguments[0]), Number(arguments[1]))),
            Function(StandardLibraryCatalog.MathTrigonometry, "degreesToRadians", static (_, arguments) => Number(arguments[0]) * Math.PI / 180d),
            Function(StandardLibraryCatalog.MathTrigonometry, "radiansToDegrees", static (_, arguments) => Number(arguments[0]) * 180d / Math.PI),
        ]
    );

    /// <summary>Gets the Math.Classification binding.</summary>
    public static DotNetStandardLibraryModuleBinding MathClassification { get; } = CreateBinding(
        StandardLibraryCatalog.MathClassification,
        [ ],
        [
            Function(StandardLibraryCatalog.MathClassification, "isFinite", static (_, arguments) => arguments[0] is long || double.IsFinite((double)arguments[0]!)),
            Function(StandardLibraryCatalog.MathClassification, "isInfinity", static (_, arguments) => arguments[0] is double value && double.IsInfinity(value)),
            Function(StandardLibraryCatalog.MathClassification, "isNaN", static (_, arguments) => arguments[0] is double.NaN),
        ]
    );

    /// <summary>Gets the Array binding.</summary>
    public static DotNetStandardLibraryModuleBinding Array { get; } = CreateBinding(
        StandardLibraryCatalog.Array,
        [ ],
        [ Function(StandardLibraryCatalog.Array, "arrayContains", ArrayContains) ]
    );

    /// <summary>Gets the Object binding.</summary>
    public static DotNetStandardLibraryModuleBinding Object { get; } = CreateBinding(
        StandardLibraryCatalog.Object,
        [ ],
        [
            Function(StandardLibraryCatalog.Object, "objectKeys", ObjectKeys),
            Function(StandardLibraryCatalog.Object, "objectValues", ObjectValues),
        ]
    );

    /// <summary>Gets the String.Inspection binding.</summary>
    public static DotNetStandardLibraryModuleBinding StringInspection { get; } = CreateBinding(
        StandardLibraryCatalog.StringInspection,
        [ ],
        [
            Function(StandardLibraryCatalog.StringInspection, "stringLength", StringLength),
            Function(StandardLibraryCatalog.StringInspection, "charAt", CharAt),
            Function(StandardLibraryCatalog.StringInspection, "isEmpty", static (_, arguments) => ((string)arguments[0]!).Length == 0),
            Function(StandardLibraryCatalog.StringInspection, "isWhiteSpace", IsWhiteSpace),
            Function(StandardLibraryCatalog.StringInspection, "stringContains", StringContains),
            Function(StandardLibraryCatalog.StringInspection, "startsWith", StartsWith),
            Function(StandardLibraryCatalog.StringInspection, "endsWith", EndsWith),
        ]
    );

    /// <summary>Gets the String.Search binding.</summary>
    public static DotNetStandardLibraryModuleBinding StringSearch { get; } = CreateBinding(
        StandardLibraryCatalog.StringSearch,
        [ ],
        [
            Function(StandardLibraryCatalog.StringSearch, "indexOf", IndexOf),
            Function(StandardLibraryCatalog.StringSearch, "lastIndexOf", LastIndexOf),
        ]
    );

    /// <summary>Gets the String.Transform binding.</summary>
    public static DotNetStandardLibraryModuleBinding StringTransform { get; } = CreateBinding(
        StandardLibraryCatalog.StringTransform,
        [ ],
        [
            Function(StandardLibraryCatalog.StringTransform, "toLower", static (_, arguments) => ((string)arguments[0]!).ToLowerInvariant()),
            Function(StandardLibraryCatalog.StringTransform, "toUpper", static (_, arguments) => ((string)arguments[0]!).ToUpperInvariant()),
            Function(StandardLibraryCatalog.StringTransform, "trim", static (_, arguments) => ((string)arguments[0]!).Trim()),
            Function(StandardLibraryCatalog.StringTransform, "trimStart", static (_, arguments) => ((string)arguments[0]!).TrimStart()),
            Function(StandardLibraryCatalog.StringTransform, "trimEnd", static (_, arguments) => ((string)arguments[0]!).TrimEnd()),
            Function(StandardLibraryCatalog.StringTransform, "repeat", Repeat),
            Function(StandardLibraryCatalog.StringTransform, "reverse", Reverse),
        ]
    );

    /// <summary>Gets the String.Slicing binding.</summary>
    public static DotNetStandardLibraryModuleBinding StringSlicing { get; } = CreateBinding(
        StandardLibraryCatalog.StringSlicing,
        [ ],
        [
            Function(StandardLibraryCatalog.StringSlicing, "substring", Substring),
            Function(StandardLibraryCatalog.StringSlicing, "remove", Remove),
            Function(StandardLibraryCatalog.StringSlicing, "insert", Insert),
        ]
    );

    /// <summary>Gets the String.Replacement binding.</summary>
    public static DotNetStandardLibraryModuleBinding StringReplacement { get; } = CreateBinding(
        StandardLibraryCatalog.StringReplacement,
        [ ],
        [
            Function(StandardLibraryCatalog.StringReplacement, "replaceFirst", ReplaceFirst),
            Function(StandardLibraryCatalog.StringReplacement, "replaceAll", ReplaceAll),
        ]
    );

    /// <summary>Gets the String.Comparison binding.</summary>
    public static DotNetStandardLibraryModuleBinding StringComparison { get; } = CreateBinding(
        StandardLibraryCatalog.StringComparison,
        [ ],
        [
            Function(StandardLibraryCatalog.StringComparison, "compareOrdinal", static (_, arguments) => NormalizeComparison(string.CompareOrdinal((string)arguments[0]!, (string)arguments[1]!))),
            Function(StandardLibraryCatalog.StringComparison, "compareIgnoreCase", static (_, arguments) => NormalizeComparison(string.Compare((string)arguments[0]!, (string)arguments[1]!, System.StringComparison.OrdinalIgnoreCase))),
            Function(StandardLibraryCatalog.StringComparison, "equalsIgnoreCase", static (_, arguments) => string.Equals((string)arguments[0]!, (string)arguments[1]!, System.StringComparison.OrdinalIgnoreCase)),
        ]
    );

    /// <summary>Gets the Parsing binding.</summary>
    public static DotNetStandardLibraryModuleBinding Parsing { get; } = CreateBinding(
        StandardLibraryCatalog.Parsing,
        [ ],
        [
            Function(StandardLibraryCatalog.Parsing, "parseInt", ParseInt),
            Function(StandardLibraryCatalog.Parsing, "parseFloat", ParseFloat),
            Function(StandardLibraryCatalog.Parsing, "parseBool", ParseBool),
        ]
    );

    /// <summary>Gets the Text.Encoding binding.</summary>
    public static DotNetStandardLibraryModuleBinding TextEncoding { get; } = CreateBinding(
        StandardLibraryCatalog.TextEncoding,
        [ ],
        [
            Function(StandardLibraryCatalog.TextEncoding, "base64Encode", Base64Encode),
            Function(StandardLibraryCatalog.TextEncoding, "base64Decode", Base64Decode),
        ]
    );

    /// <summary>Gets every deterministic binding in catalog order.</summary>
    public static IReadOnlyList<DotNetStandardLibraryModuleBinding> Deterministic { get; } =
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
        TextEncoding,
    ];

    /// <summary>Creates a Random module binding.</summary>
    /// <param name="options">The random configuration.</param>
    /// <returns>The configured binding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is <c>null</c>.</exception>
    public static DotNetStandardLibraryModuleBinding CreateRandom(RandomStandardLibraryOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        IStandardLibraryRandomSource randomSource = options.RandomSource ?? DefaultRandomSource.Instance;

        return CreateBinding(
            StandardLibraryCatalog.Random,
            [ ],
            [
                Function(StandardLibraryCatalog.Random, "randomFloat", (context, _) => RandomFloat(context, randomSource)),
                Function(StandardLibraryCatalog.Random, "randomInt", (context, arguments) => RandomInt(context, arguments, randomSource)),
            ]
        );
    }

    /// <summary>Creates a Clock module binding.</summary>
    /// <param name="options">The clock configuration.</param>
    /// <returns>The configured binding.</returns>
    /// <exception cref="ArgumentException">Thrown when the configured time provider is <c>null</c>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is <c>null</c>.</exception>
    public static DotNetStandardLibraryModuleBinding CreateClock(ClockStandardLibraryOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        TimeProvider timeProvider = options.TimeProvider ??
            throw new ArgumentException("The time provider cannot be null.", nameof(options));

        return CreateBinding(
            StandardLibraryCatalog.Clock,
            [ ],
            [
                Function(StandardLibraryCatalog.Clock, "unixTimeSeconds", (context, _) => UnixTime(context, timeProvider, false)),
                Function(StandardLibraryCatalog.Clock, "unixTimeMilliseconds", (context, _) => UnixTime(context, timeProvider, true)),
            ]
        );
    }

    /// <summary>Creates a Guid module binding.</summary>
    /// <param name="options">The GUID configuration.</param>
    /// <returns>The configured binding.</returns>
    /// <exception cref="ArgumentException">Thrown when the configured time provider is <c>null</c>.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options" /> is <c>null</c>.</exception>
    public static DotNetStandardLibraryModuleBinding CreateGuid(GuidStandardLibraryOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        TimeProvider timeProvider = options.TimeProvider ??
            throw new ArgumentException("The time provider cannot be null.", nameof(options));

        return CreateBinding(
            StandardLibraryCatalog.Guid,
            [ ],
            [
                Function(StandardLibraryCatalog.Guid, "newGuid", static (_, _) => Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture)),
                Function(StandardLibraryCatalog.Guid, "newGuidV7", (context, _) => NewGuidV7(context, timeProvider)),
                Function(StandardLibraryCatalog.Guid, "isGuid", static (_, arguments) => IsCanonicalGuid((string)arguments[0]!)),
            ]
        );
    }

    private static object Abs(DotNetProviderInvocationContext context, IReadOnlyList<object?> arguments)
    {
        if (arguments[0] is double floating)
        {
            return Math.Abs(floating);
        }

        long integer = (long)arguments[0]!;

        if (integer == long.MinValue)
        {
            context.ThrowApplicationError(OverflowError, "The absolute value exceeds the signed integer range.");
        }

        return Math.Abs(integer);
    }

    private static object Sign(DotNetProviderInvocationContext context, IReadOnlyList<object?> arguments)
    {
        if (arguments[0] is double floating)
        {
            if (double.IsNaN(floating))
            {
                context.ThrowApplicationError(InvalidArgumentError, "NaN does not have a sign.");
            }

            return (long)Math.Sign(floating);
        }

        return (long)Math.Sign((long)arguments[0]!);
    }

    private static object Min(DotNetProviderInvocationContext _, IReadOnlyList<object?> arguments)
    {
        if (arguments[0] is long left && arguments[1] is long right)
        {
            return Math.Min(left, right);
        }

        return Math.Min(Number(arguments[0]), Number(arguments[1]));
    }

    private static object Max(DotNetProviderInvocationContext _, IReadOnlyList<object?> arguments)
    {
        if (arguments[0] is long left && arguments[1] is long right)
        {
            return Math.Max(left, right);
        }

        return Math.Max(Number(arguments[0]), Number(arguments[1]));
    }

    private static object Clamp(DotNetProviderInvocationContext context, IReadOnlyList<object?> arguments)
    {
        if (arguments[0] is long value && arguments[1] is long minimum && arguments[2] is long maximum)
        {
            if (minimum > maximum)
            {
                context.ThrowApplicationError(RangeError, "The minimum cannot exceed the maximum.");
            }

            return Math.Clamp(value, minimum, maximum);
        }

        double floatingValue = Number(arguments[0]);
        double floatingMinimum = Number(arguments[1]);
        double floatingMaximum = Number(arguments[2]);

        if (double.IsNaN(floatingMinimum) || double.IsNaN(floatingMaximum) || floatingMinimum > floatingMaximum)
        {
            context.ThrowApplicationError(RangeError, "The minimum and maximum must define an ordered range.");
        }

        return Math.Clamp(floatingValue, floatingMinimum, floatingMaximum);
    }

    private static object Floor(DotNetProviderInvocationContext _, IReadOnlyList<object?> arguments)
    {
        return arguments[0] is long ? arguments[0]! : Math.Floor((double)arguments[0]!);
    }

    private static object Ceiling(DotNetProviderInvocationContext _, IReadOnlyList<object?> arguments)
    {
        return arguments[0] is long ? arguments[0]! : Math.Ceiling((double)arguments[0]!);
    }

    private static object Truncate(DotNetProviderInvocationContext _, IReadOnlyList<object?> arguments)
    {
        return arguments[0] is long ? arguments[0]! : Math.Truncate((double)arguments[0]!);
    }

    private static object Round(DotNetProviderInvocationContext _, IReadOnlyList<object?> arguments)
    {
        return arguments[0] is long ? arguments[0]! : Math.Round((double)arguments[0]!, MidpointRounding.ToEven);
    }

    private static object TruncateToInt(DotNetProviderInvocationContext context, IReadOnlyList<object?> arguments)
    {
        double value = (double)arguments[0]!;

        if (!double.IsFinite(value) || value < long.MinValue || value >= 9223372036854775808d)
        {
            context.ThrowApplicationError(ConversionError, "The floating-point value cannot be converted to an integer.");
        }

        return (long)value;
    }

    private static object ArrayContains(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        object array = arguments[0]!;
        int count = context.GetReadOnlyArrayCount(array);

        for (int index = 0; index < count; index++)
        {
            context.ThrowIfCancellationRequested();

            if (!context.TryGetReadOnlyArrayElement(array, index, out object? element))
            {
                throw new InvalidOperationException(
                    "The array adapter failed to return a declared element."
                );
            }

            if (context.StructuralEquals(element, arguments[1]))
            {
                return true;
            }
        }

        return false;
    }

    private static object ObjectKeys(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string[] names = GetSortedPropertyNames(context, arguments[0]!);
        return new DotNetStandardLibraryReadOnlyArray(names);
    }

    private static object ObjectValues(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        object value = arguments[0]!;
        string[] names = GetSortedPropertyNames(context, value);
        object?[] values = new object?[names.Length];

        for (int index = 0; index < names.Length; index++)
        {
            context.ThrowIfCancellationRequested();

            if (!context.TryGetObjectProperty(value, names[index], out values[index]))
            {
                throw new InvalidOperationException(
                    $"Object property '{names[index]}' disappeared during enumeration."
                );
            }
        }

        return new DotNetStandardLibraryReadOnlyArray(values);
    }

    private static string[] GetSortedPropertyNames(
        DotNetProviderInvocationContext context,
        object value
    )
    {
        List<string> names = [ ];

        foreach (string name in context.GetObjectPropertyNames(value))
        {
            context.ThrowIfCancellationRequested();

            if (name is null)
            {
                throw new InvalidOperationException(
                    "An object adapter returned a null property name."
                );
            }

            names.Add(name);
        }

        names.Sort(StringComparer.Ordinal);

        for (int index = 1; index < names.Count; index++)
        {
            if (string.Equals(names[index - 1], names[index], System.StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Object property '{names[index]}' was enumerated more than once."
                );
            }
        }

        return [ .. names ];
    }

    private static object StringLength(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        long count = 0;

        foreach (Rune _ in ((string)arguments[0]!).EnumerateRunes())
        {
            context.ThrowIfCancellationRequested();
            count++;
        }

        return count;
    }

    private static object CharAt(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        long requested = (long)arguments[1]!;

        if (requested < 0)
        {
            context.ThrowApplicationError(RangeError, "The string index cannot be negative.");
        }

        long index = 0;

        foreach (Rune rune in ((string)arguments[0]!).EnumerateRunes())
        {
            context.ThrowIfCancellationRequested();

            if (index == requested)
            {
                return (long)rune.Value;
            }

            index++;
        }

        context.ThrowApplicationError(RangeError, "The string index is outside the scalar range.");
        throw new InvalidOperationException();
    }

    private static object IsWhiteSpace(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        foreach (Rune rune in ((string)arguments[0]!).EnumerateRunes())
        {
            context.ThrowIfCancellationRequested();

            if (!Rune.IsWhiteSpace(rune))
            {
                return false;
            }
        }

        return true;
    }

    private static object? IndexOf(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        int index = FindOrdinal(context, value, (string)arguments[1]!, false);
        return index < 0 ? null : CountRunes(context, value.AsSpan(0, index));
    }

    private static object? LastIndexOf(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        int index = FindOrdinal(context, value, (string)arguments[1]!, true);
        return index < 0 ? null : CountRunes(context, value.AsSpan(0, index));
    }

    private static object StringContains(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        return FindOrdinal(
            context,
            (string)arguments[0]!,
            (string)arguments[1]!,
            false
        ) >= 0;
    }

    private static object StartsWith(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        string prefix = (string)arguments[1]!;
        return prefix.Length <= value.Length && MatchesAt(context, value, prefix, 0);
    }

    private static object EndsWith(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        string suffix = (string)arguments[1]!;
        return suffix.Length <= value.Length &&
            MatchesAt(context, value, suffix, value.Length - suffix.Length);
    }

    private static object Repeat(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        long count = (long)arguments[1]!;

        if (count < 0)
        {
            context.ThrowApplicationError(RangeError, "The repeat count cannot be negative.");
        }

        int capacity;

        try
        {
            capacity = checked(value.Length * checked((int)count));
        }
        catch (OverflowException)
        {
            context.ThrowApplicationError(OverflowError, "The repeated string length exceeds the supported range.");
            throw;
        }

        try
        {
            StringBuilder builder = new (capacity);

            for (long index = 0; index < count; index++)
            {
                context.ThrowIfCancellationRequested();
                builder.Append(value);
            }

            return builder.ToString();
        }
        catch (ArgumentOutOfRangeException)
        {
            context.ThrowApplicationError(ResourceError, "The repeated string cannot be allocated.");
            throw;
        }
        catch (OutOfMemoryException)
        {
            context.ThrowApplicationError(ResourceError, "The repeated string cannot be allocated.");
            throw;
        }
    }

    private static object Reverse(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        Rune[] runes = [ .. value.EnumerateRunes() ];
        StringBuilder builder = new (value.Length);

        for (int index = runes.Length - 1; index >= 0; index--)
        {
            context.ThrowIfCancellationRequested();
            builder.Append(runes[index].ToString());
        }

        return builder.ToString();
    }

    private static object Substring(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        (int start, int end) = GetUtf16Range(context, value, (long)arguments[1]!, (long)arguments[2]!);
        return value[start..end];
    }

    private static object Remove(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        (int start, int end) = GetUtf16Range(context, value, (long)arguments[1]!, (long)arguments[2]!);
        return value.Remove(start, end - start);
    }

    private static object Insert(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        int index = GetUtf16Index(context, value, (long)arguments[1]!, true);
        return value.Insert(index, (string)arguments[2]!);
    }

    private static object ReplaceFirst(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        string oldValue = (string)arguments[1]!;
        string newValue = (string)arguments[2]!;

        if (oldValue.Length == 0)
        {
            context.ThrowApplicationError(InvalidArgumentError, "The replaced value cannot be empty.");
        }

        int index = FindOrdinal(context, value, oldValue, false);
        return index < 0
            ? value
            : string.Concat(value.AsSpan(0, index), newValue, value.AsSpan(index + oldValue.Length));
    }

    private static object ReplaceAll(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string oldValue = (string)arguments[1]!;

        if (oldValue.Length == 0)
        {
            context.ThrowApplicationError(InvalidArgumentError, "The replaced value cannot be empty.");
        }

        string value = (string)arguments[0]!;
        string newValue = (string)arguments[2]!;
        int firstIndex = FindOrdinal(context, value, oldValue, false);

        if (firstIndex < 0)
        {
            return value;
        }

        try
        {
            StringBuilder builder = new (value.Length);
            int copiedThrough = 0;
            int matchIndex = firstIndex;

            while (matchIndex >= 0)
            {
                context.ThrowIfCancellationRequested();
                builder.Append(value, copiedThrough, matchIndex - copiedThrough);
                builder.Append(newValue);
                copiedThrough = matchIndex + oldValue.Length;
                matchIndex = FindOrdinal(
                    context,
                    value,
                    oldValue,
                    false,
                    copiedThrough
                );
            }

            builder.Append(value, copiedThrough, value.Length - copiedThrough);
            return builder.ToString();
        }
        catch (ArgumentOutOfRangeException)
        {
            context.ThrowApplicationError(ResourceError, "The replaced string cannot be allocated.");
            throw;
        }
        catch (OutOfMemoryException)
        {
            context.ThrowApplicationError(ResourceError, "The replaced string cannot be allocated.");
            throw;
        }
    }

    private static object? ParseInt(
        DotNetProviderInvocationContext _,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;

        if (value.Length == 0 || value.AsSpan().Trim().Length != value.Length)
        {
            return null;
        }

        int sign = 1;
        int offset = 0;

        if (value[0] is '+' or '-')
        {
            sign = value[0] == '-' ? -1 : 1;
            offset = 1;

            if (offset == value.Length)
            {
                return null;
            }
        }

        int numberBase = 10;

        if (
            offset + 2 <= value.Length &&
            value[offset] == '0' &&
            value[offset + 1] is 'b' or 'B' or 'o' or 'O' or 'x' or 'X'
        )
        {
            numberBase = value[offset + 1] switch
            {
                'b' or 'B' => 2,
                'o' or 'O' => 8,
                _ => 16,
            };
            offset += 2;

            if (offset == value.Length)
            {
                return null;
            }
        }

        ulong magnitude = 0;

        for (int index = offset; index < value.Length; index++)
        {
            int digit = HexDigit(value[index]);

            if (digit < 0 || digit >= numberBase)
            {
                return null;
            }

            try
            {
                magnitude = checked(magnitude * (uint)numberBase + (uint)digit);
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        if (sign < 0)
        {
            if (magnitude > 9223372036854775808UL)
            {
                return null;
            }

            return magnitude == 9223372036854775808UL ? long.MinValue : -(long)magnitude;
        }

        return magnitude <= long.MaxValue ? (long)magnitude : null;
    }

    private static object? ParseFloat(
        DotNetProviderInvocationContext _,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;

        if (value is "nan")
        {
            return double.NaN;
        }

        if (value is "infty")
        {
            return double.PositiveInfinity;
        }

        if (value is "-infty")
        {
            return double.NegativeInfinity;
        }

        if (!IsFloatSyntax(value))
        {
            return null;
        }

        if (
            !double.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
                CultureInfo.InvariantCulture,
                out double result
            ) ||
            !double.IsFinite(result)
        )
        {
            return null;
        }

        return result;
    }

    private static object? ParseBool(
        DotNetProviderInvocationContext _,
        IReadOnlyList<object?> arguments
    )
    {
        return arguments[0] switch
        {
            "true" => true,
            "false" => false,
            _ => null,
        };
    }

    private static object Base64Encode(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;
        ObserveString(context, value);

        try
        {
            byte[] bytes = StrictUtf8.GetBytes(value);
            context.ThrowIfCancellationRequested();
            string result = Convert.ToBase64String(bytes);
            context.ThrowIfCancellationRequested();
            return result;
        }
        catch (EncoderFallbackException)
        {
            context.ThrowApplicationError(ConversionError, "The string cannot be encoded as UTF-8.");
            throw;
        }
        catch (OutOfMemoryException)
        {
            context.ThrowApplicationError(ResourceError, "The Base64 result cannot be allocated.");
            throw;
        }
    }

    private static object? Base64Decode(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments
    )
    {
        string value = (string)arguments[0]!;

        if (!IsStandardBase64(context, value))
        {
            return null;
        }

        try
        {
            byte[] bytes = new byte[checked(value.Length / 4 * 3)];

            if (!Convert.TryFromBase64String(value, bytes, out int bytesWritten))
            {
                return null;
            }

            context.ThrowIfCancellationRequested();
            string result = StrictUtf8.GetString(bytes, 0, bytesWritten);
            context.ThrowIfCancellationRequested();
            return result;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        catch (OutOfMemoryException)
        {
            context.ThrowApplicationError(ResourceError, "The decoded string cannot be allocated.");
            throw;
        }
    }

    private static object RandomFloat(
        DotNetProviderInvocationContext context,
        IStandardLibraryRandomSource randomSource
    )
    {
        double value = randomSource.NextDouble();

        if (value is < 0d or >= 1d or double.NaN)
        {
            throw new InvalidOperationException(
                "The random source returned a value outside [0, 1)."
            );
        }

        return value;
    }

    private static object RandomInt(
        DotNetProviderInvocationContext context,
        IReadOnlyList<object?> arguments,
        IStandardLibraryRandomSource randomSource
    )
    {
        long minimum = (long)arguments[0]!;
        long maximum = (long)arguments[1]!;

        if (minimum >= maximum)
        {
            context.ThrowApplicationError(RangeError, "The random integer range must be non-empty and ordered.");
        }

        long result = randomSource.NextInt64(minimum, maximum);

        if (result < minimum || result >= maximum)
        {
            throw new InvalidOperationException(
                "The random source returned a value outside the requested range."
            );
        }

        return result;
    }

    private static object UnixTime(
        DotNetProviderInvocationContext context,
        TimeProvider timeProvider,
        bool milliseconds
    )
    {
        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            return milliseconds ? now.ToUnixTimeMilliseconds() : now.ToUnixTimeSeconds();
        }
        catch (ArgumentOutOfRangeException)
        {
            context.ThrowApplicationError(ConversionError, "The configured clock value cannot be represented as Unix time.");
            throw;
        }
    }

    private static object NewGuidV7(
        DotNetProviderInvocationContext context,
        TimeProvider timeProvider
    )
    {
        try
        {
            return Guid.CreateVersion7(timeProvider.GetUtcNow()).ToString("D", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            context.ThrowApplicationError(ConversionError, "The configured clock value cannot be represented by a Version 7 GUID.");
            throw;
        }
    }

    private static bool IsCanonicalGuid(string value)
    {
        return value.Length == 36 &&
            Guid.TryParseExact(value, "D", out Guid parsed) &&
            string.Equals(parsed.ToString("D", CultureInfo.InvariantCulture), value, System.StringComparison.OrdinalIgnoreCase);
    }

    private static (int Start, int End) GetUtf16Range(
        DotNetProviderInvocationContext context,
        string value,
        long start,
        long length
    )
    {
        if (start < 0 || length < 0)
        {
            context.ThrowApplicationError(RangeError, "String slice indexes and lengths cannot be negative.");
        }

        long end;

        try
        {
            end = checked(start + length);
        }
        catch (OverflowException)
        {
            context.ThrowApplicationError(OverflowError, "The string slice range overflowed.");
            throw;
        }

        int utf16Start = GetUtf16Index(context, value, start, true);
        int utf16End = GetUtf16Index(context, value, end, true);
        return (utf16Start, utf16End);
    }

    private static int GetUtf16Index(
        DotNetProviderInvocationContext context,
        string value,
        long scalarIndex,
        bool allowEnd
    )
    {
        if (scalarIndex < 0)
        {
            context.ThrowApplicationError(RangeError, "The string index cannot be negative.");
        }

        long currentScalar = 0;
        int utf16Index = 0;

        foreach (Rune rune in value.EnumerateRunes())
        {
            context.ThrowIfCancellationRequested();

            if (currentScalar == scalarIndex)
            {
                return utf16Index;
            }

            utf16Index += rune.Utf16SequenceLength;
            currentScalar++;
        }

        if (allowEnd && currentScalar == scalarIndex)
        {
            return value.Length;
        }

        context.ThrowApplicationError(RangeError, "The string index is outside the scalar range.");
        throw new InvalidOperationException();
    }

    private static long CountRunes(
        DotNetProviderInvocationContext context,
        ReadOnlySpan<char> value
    )
    {
        long count = 0;
        int consumed = 0;

        while (consumed < value.Length)
        {
            context.ThrowIfCancellationRequested();
            OperationStatus status = Rune.DecodeFromUtf16(value[consumed..], out _, out int charsConsumed);
            consumed += status == OperationStatus.Done ? charsConsumed : 1;
            count++;
        }

        return count;
    }

    private static bool IsFloatSyntax(string value)
    {
        if (value.Length == 0 || value.AsSpan().Trim().Length != value.Length)
        {
            return false;
        }

        int index = value[0] is '+' or '-' ? 1 : 0;
        int integerStart = index;

        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            index++;
        }

        if (index == integerStart)
        {
            return false;
        }

        if (index < value.Length && value[index] == '.')
        {
            index++;
            int fractionStart = index;

            while (index < value.Length && char.IsAsciiDigit(value[index]))
            {
                index++;
            }

            if (index == fractionStart)
            {
                return false;
            }
        }

        if (index < value.Length && value[index] is 'e' or 'E')
        {
            index++;

            if (index < value.Length && value[index] is '+' or '-')
            {
                index++;
            }

            int exponentStart = index;

            while (index < value.Length && char.IsAsciiDigit(value[index]))
            {
                index++;
            }

            if (index == exponentStart)
            {
                return false;
            }
        }

        return index == value.Length;
    }

    private static bool IsStandardBase64(
        DotNetProviderInvocationContext context,
        string value
    )
    {
        if (value.Length % 4 != 0)
        {
            return false;
        }

        int paddingStart = value.Length;

        for (int index = 0; index < value.Length; index++)
        {
            context.ThrowIfCancellationRequested();

            if (value[index] == '=')
            {
                paddingStart = index;
                break;
            }
        }

        if (
            value.Length - paddingStart > 2 ||
            value.AsSpan(paddingStart).IndexOfAnyExcept('=') >= 0
        )
        {
            return false;
        }

        for (int index = 0; index < paddingStart; index++)
        {
            context.ThrowIfCancellationRequested();
            char character = value[index];

            if (Base64DigitValue(character) < 0)
            {
                return false;
            }
        }

        int paddingLength = value.Length - paddingStart;

        if (
            paddingLength == 2 &&
            (Base64DigitValue(value[^3]) & 0x0f) != 0
        )
        {
            return false;
        }

        if (
            paddingLength == 1 &&
            (Base64DigitValue(value[^2]) & 0x03) != 0
        )
        {
            return false;
        }

        return true;
    }

    private static int Base64DigitValue(char value)
    {
        return value switch
        {
            >= 'A' and <= 'Z' => value - 'A',
            >= 'a' and <= 'z' => value - 'a' + 26,
            >= '0' and <= '9' => value - '0' + 52,
            '+' => 62,
            '/' => 63,
            _ => -1,
        };
    }

    private static int FindOrdinal(
        DotNetProviderInvocationContext context,
        string value,
        string part,
        bool last,
        int start = 0
    )
    {
        if (part.Length == 0)
        {
            return last ? value.Length : start;
        }

        int maximum = value.Length - part.Length;

        if (last)
        {
            for (int index = maximum; index >= start; index--)
            {
                if (MatchesAt(context, value, part, index))
                {
                    return index;
                }
            }

            return -1;
        }

        for (int index = start; index <= maximum; index++)
        {
            if (MatchesAt(context, value, part, index))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool MatchesAt(
        DotNetProviderInvocationContext context,
        string value,
        string part,
        int index
    )
    {
        for (int partIndex = 0; partIndex < part.Length; partIndex++)
        {
            context.ThrowIfCancellationRequested();

            if (value[index + partIndex] != part[partIndex])
            {
                return false;
            }
        }

        return true;
    }

    private static void ObserveString(
        DotNetProviderInvocationContext context,
        string value
    )
    {
        for (int index = 0; index < value.Length; index += 4096)
        {
            context.ThrowIfCancellationRequested();
        }
    }

    private static int HexDigit(char value)
    {
        return value switch
        {
            >= '0' and <= '9' => value - '0',
            >= 'a' and <= 'f' => value - 'a' + 10,
            >= 'A' and <= 'F' => value - 'A' + 10,
            _ => -1,
        };
    }

    private static long NormalizeComparison(int value)
    {
        return Math.Sign(value);
    }

    private static double Number(object? value)
    {
        return value is long integer ? integer : (double)value!;
    }

    private static KeyValuePair<string, object?> Global(
        StandardLibraryModule module,
        string name,
        object? value
    )
    {
        GlobalSymbol declaration = module.Globals.Single(
            global => string.Equals(global.Name, name, System.StringComparison.Ordinal)
        );
        return KeyValuePair.Create(declaration.Id, value);
    }

    private static DotNetStandardLibraryFunction Function(
        StandardLibraryModule module,
        string name,
        DotNetProviderFunction implementation
    )
    {
        FunctionSymbol declaration = module.Functions.Single(
            function => string.Equals(function.Name, name, System.StringComparison.Ordinal)
        );
        return new DotNetStandardLibraryFunction(
            declaration.Id,
            declaration.Parameters.Count,
            implementation
        );
    }

    private static DotNetStandardLibraryModuleBinding CreateBinding(
        StandardLibraryModule module,
        IEnumerable<KeyValuePair<string, object?>> globals,
        IEnumerable<DotNetStandardLibraryFunction> functions
    )
    {
        return new DotNetStandardLibraryModuleBinding(module.Id, module, globals, functions);
    }

    private sealed class DefaultRandomSource : IStandardLibraryRandomSource
    {
        private readonly Lock syncRoot = new ();
        private readonly Random random = new ();

        public static DefaultRandomSource Instance { get; } = new ();

        public double NextDouble()
        {
            lock (syncRoot)
            {
                return random.NextDouble();
            }
        }

        public long NextInt64(long minimum, long maximum)
        {
            lock (syncRoot)
            {
                return random.NextInt64(minimum, maximum);
            }
        }
    }
}
