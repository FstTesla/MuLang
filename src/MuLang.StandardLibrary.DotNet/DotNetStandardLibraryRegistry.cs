using MuLang.Core.Types;
using MuLang.Exporters.DotNet;
using System.Buffers;
using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace MuLang.StandardLibrary.DotNet;

internal static class DotNetStandardLibraryRegistry
{
    private const string ConversionError = "mulang.std.conversion";
    private const string InvalidArgumentError = "mulang.std.invalid_argument";
    private const string OverflowError = "mulang.std.overflow";
    private const string RangeError = "mulang.std.range";
    private const string ResourceError = "mulang.std.resource";
    private static readonly UTF8Encoding StrictUtf8 = new (false, true);

    public static IStandardLibraryRandomSource DefaultRandomSource { get; } =
        new ThreadSafeRandomSource();

    public static IReadOnlyDictionary<string, object?> Globals { get; } =
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [StandardLibraryCatalog.Globals.E.Id] = Math.E,
            [StandardLibraryCatalog.Globals.Pi.Id] = Math.PI,
            [StandardLibraryCatalog.Globals.Tau.Id] = Math.Tau,
            [StandardLibraryCatalog.Globals.MinInt.Id] = long.MinValue,
            [StandardLibraryCatalog.Globals.MaxInt.Id] = long.MaxValue,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyDictionary<
        string,
        Func<DotNetStandardLibraryServices, DotNetProviderFunction>
    > Functions { get; } = CreateFunctions();

    static DotNetStandardLibraryRegistry()
    {
        ValidateParity();
    }

    private static IReadOnlyDictionary<
        string,
        Func<DotNetStandardLibraryServices, DotNetProviderFunction>
    > CreateFunctions()
    {
        Dictionary<string, Func<DotNetStandardLibraryServices, DotNetProviderFunction>> functions =
            new (StringComparer.Ordinal)
            {
                [StandardLibraryCatalog.Functions.Abs.Id] = static _ => Abs,
                [StandardLibraryCatalog.Functions.Sign.Id] = static _ => Sign,
                [StandardLibraryCatalog.Functions.Min.Id] = static _ => Min,
                [StandardLibraryCatalog.Functions.Max.Id] = static _ => Max,
                [StandardLibraryCatalog.Functions.Clamp.Id] = static _ => Clamp,
                [StandardLibraryCatalog.Functions.Floor.Id] = static _ => Floor,
                [StandardLibraryCatalog.Functions.Ceiling.Id] = static _ => Ceiling,
                [StandardLibraryCatalog.Functions.Truncate.Id] = static _ => Truncate,
                [StandardLibraryCatalog.Functions.Round.Id] = static _ => Round,
                [StandardLibraryCatalog.Functions.TruncateToInt.Id] = static _ => TruncateToInt,
                [StandardLibraryCatalog.Functions.Sqrt.Id] = static _ => static (_, arguments) => Math.Sqrt(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Pow.Id] = static _ => static (_, arguments) => Math.Pow(Number(arguments[0]), Number(arguments[1])),
                [StandardLibraryCatalog.Functions.Exp.Id] = static _ => static (_, arguments) => Math.Exp(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Log.Id] = static _ => static (_, arguments) => Math.Log(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Log10.Id] = static _ => static (_, arguments) => Math.Log10(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Sin.Id] = static _ => static (_, arguments) => Math.Sin(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Cos.Id] = static _ => static (_, arguments) => Math.Cos(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Tan.Id] = static _ => static (_, arguments) => Math.Tan(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Asin.Id] = static _ => static (_, arguments) => Math.Asin(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Acos.Id] = static _ => static (_, arguments) => Math.Acos(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Atan.Id] = static _ => static (_, arguments) => Math.Atan(Number(arguments[0])),
                [StandardLibraryCatalog.Functions.Atan2.Id] = static _ => static (_, arguments) => Math.Atan2(Number(arguments[0]), Number(arguments[1])),
                [StandardLibraryCatalog.Functions.DegreesToRadians.Id] = static _ => static (_, arguments) => Number(arguments[0]) * Math.PI / 180d,
                [StandardLibraryCatalog.Functions.RadiansToDegrees.Id] = static _ => static (_, arguments) => Number(arguments[0]) * 180d / Math.PI,
                [StandardLibraryCatalog.Functions.IsFinite.Id] = static _ => static (_, arguments) => arguments[0] is long || double.IsFinite((double)arguments[0]!),
                [StandardLibraryCatalog.Functions.IsInfinity.Id] = static _ => static (_, arguments) => arguments[0] is double value && double.IsInfinity(value),
                [StandardLibraryCatalog.Functions.IsNaN.Id] = static _ => static (_, arguments) => arguments[0] is double.NaN,
                [StandardLibraryCatalog.Functions.ArrayContains.Id] = static _ => ArrayContains,
                [StandardLibraryCatalog.Functions.ObjectKeys.Id] = static _ => ObjectKeys,
                [StandardLibraryCatalog.Functions.ObjectValues.Id] = static _ => ObjectValues,
                [StandardLibraryCatalog.Functions.StringLength.Id] = static _ => StringLength,
                [StandardLibraryCatalog.Functions.CharAt.Id] = static _ => CharAt,
                [StandardLibraryCatalog.Functions.IsEmpty.Id] = static _ => static (_, arguments) => ((string)arguments[0]!).Length == 0,
                [StandardLibraryCatalog.Functions.IsWhiteSpace.Id] = static _ => IsWhiteSpace,
                [StandardLibraryCatalog.Functions.StringContains.Id] = static _ => StringContains,
                [StandardLibraryCatalog.Functions.StartsWith.Id] = static _ => StartsWith,
                [StandardLibraryCatalog.Functions.EndsWith.Id] = static _ => EndsWith,
                [StandardLibraryCatalog.Functions.IndexOf.Id] = static _ => IndexOf,
                [StandardLibraryCatalog.Functions.LastIndexOf.Id] = static _ => LastIndexOf,
                [StandardLibraryCatalog.Functions.ToLower.Id] = static _ => static (_, arguments) => ((string)arguments[0]!).ToLowerInvariant(),
                [StandardLibraryCatalog.Functions.ToUpper.Id] = static _ => static (_, arguments) => ((string)arguments[0]!).ToUpperInvariant(),
                [StandardLibraryCatalog.Functions.Trim.Id] = static _ => static (_, arguments) => ((string)arguments[0]!).Trim(),
                [StandardLibraryCatalog.Functions.TrimStart.Id] = static _ => static (_, arguments) => ((string)arguments[0]!).TrimStart(),
                [StandardLibraryCatalog.Functions.TrimEnd.Id] = static _ => static (_, arguments) => ((string)arguments[0]!).TrimEnd(),
                [StandardLibraryCatalog.Functions.Repeat.Id] = static _ => Repeat,
                [StandardLibraryCatalog.Functions.Reverse.Id] = static _ => Reverse,
                [StandardLibraryCatalog.Functions.Substring.Id] = static _ => Substring,
                [StandardLibraryCatalog.Functions.Remove.Id] = static _ => Remove,
                [StandardLibraryCatalog.Functions.Insert.Id] = static _ => Insert,
                [StandardLibraryCatalog.Functions.ReplaceFirst.Id] = static _ => ReplaceFirst,
                [StandardLibraryCatalog.Functions.ReplaceAll.Id] = static _ => ReplaceAll,
                [StandardLibraryCatalog.Functions.CompareOrdinal.Id] = static _ => static (_, arguments) => NormalizeComparison(string.CompareOrdinal((string)arguments[0]!, (string)arguments[1]!)),
                [StandardLibraryCatalog.Functions.CompareIgnoreCase.Id] = static _ => static (_, arguments) => NormalizeComparison(string.Compare((string)arguments[0]!, (string)arguments[1]!, StringComparison.OrdinalIgnoreCase)),
                [StandardLibraryCatalog.Functions.EqualsIgnoreCase.Id] = static _ => static (_, arguments) => string.Equals((string)arguments[0]!, (string)arguments[1]!, StringComparison.OrdinalIgnoreCase),
                [StandardLibraryCatalog.Functions.ParseInt.Id] = static _ => ParseInt,
                [StandardLibraryCatalog.Functions.ParseFloat.Id] = static _ => ParseFloat,
                [StandardLibraryCatalog.Functions.ParseBool.Id] = static _ => ParseBool,
                [StandardLibraryCatalog.Functions.RandomFloat.Id] = static services => (context, _) => RandomFloat(context, services.RandomSource),
                [StandardLibraryCatalog.Functions.RandomInt.Id] = static services => (context, arguments) => RandomInt(context, arguments, services.RandomSource),
                [StandardLibraryCatalog.Functions.UnixTimeSeconds.Id] = static services =>
                {
                    TimeProvider timeProvider = services.GetClockTimeProvider();
                    return (context, _) => UnixTime(context, timeProvider, false);
                },
                [StandardLibraryCatalog.Functions.UnixTimeMilliseconds.Id] = static services =>
                {
                    TimeProvider timeProvider = services.GetClockTimeProvider();
                    return (context, _) => UnixTime(context, timeProvider, true);
                },
                [StandardLibraryCatalog.Functions.NewGuid.Id] = static _ => static (_, _) => Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
                [StandardLibraryCatalog.Functions.NewGuidV7.Id] = static services =>
                {
                    TimeProvider timeProvider = services.GetGuidTimeProvider();
                    return (context, _) => NewGuidV7(context, timeProvider);
                },
                [StandardLibraryCatalog.Functions.IsGuid.Id] = static _ => static (_, arguments) => IsCanonicalGuid((string)arguments[0]!),
                [StandardLibraryCatalog.Functions.Base64Encode.Id] = static _ => Base64Encode,
                [StandardLibraryCatalog.Functions.Base64Decode.Id] = static _ => Base64Decode,
            };

        return functions.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void ValidateParity()
    {
        ISet<string> declaredGlobals = StandardLibraryCatalog.Globals.All
            .Select(static global => global.Id)
            .ToHashSet(StringComparer.Ordinal);
        ISet<string> declaredFunctions = StandardLibraryCatalog.Functions.All
            .Select(static function => function.Id)
            .ToHashSet(StringComparer.Ordinal);

        if (!declaredGlobals.SetEquals(Globals.Keys))
        {
            throw new InvalidOperationException(
                "The .NET standard-library global registry does not match the declaration catalog."
            );
        }

        if (!declaredFunctions.SetEquals(Functions.Keys))
        {
            throw new InvalidOperationException(
                "The .NET standard-library function registry does not match the declaration catalog."
            );
        }

        foreach (StandardLibraryGlobal global in StandardLibraryCatalog.Globals.All)
        {
            if (!IsCompatibleGlobalValue(global.Declaration.Type, Globals[global.Id]))
            {
                throw new InvalidOperationException(
                    $"The .NET standard-library global '{global.Id}' is incompatible with its declaration."
                );
            }
        }
    }

    private static bool IsCompatibleGlobalValue(TypeSymbol type, object? value)
    {
        if (ReferenceEquals(type, TypeSymbols.Bool))
        {
            return value is bool;
        }

        if (ReferenceEquals(type, TypeSymbols.Int))
        {
            return value is long;
        }

        if (ReferenceEquals(type, TypeSymbols.Float))
        {
            return value is double;
        }

        if (ReferenceEquals(type, TypeSymbols.Number))
        {
            return value is long or double;
        }

        if (ReferenceEquals(type, TypeSymbols.String))
        {
            return value is string;
        }

        return false;
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
            if (string.Equals(names[index - 1], names[index], StringComparison.Ordinal))
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
            string.Equals(parsed.ToString("D", CultureInfo.InvariantCulture), value, StringComparison.OrdinalIgnoreCase);
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

    private sealed class ThreadSafeRandomSource : IStandardLibraryRandomSource
    {
        private readonly Lock syncRoot = new ();
        private readonly Random random = new ();

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
