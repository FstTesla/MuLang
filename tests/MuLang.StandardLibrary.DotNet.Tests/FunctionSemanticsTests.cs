using MuLang.Core.Runtime;
using MuLang.Exporters.DotNet;
using System.Globalization;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class FunctionSemanticsTests
{
    [Test]
    public void MathFunctionsPreserveNumericSemantics()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(Invoke(DotNetStandardLibraryModules.MathBasic, "abs", -4L), Is.EqualTo(4L));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathBasic, "abs", -4.5d), Is.EqualTo(4.5d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathBasic, "sign", -0.5d), Is.EqualTo(-1L));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathBasic, "min", 4L, 2L), Is.EqualTo(2L));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathBasic, "max", 4L, 2.5d), Is.EqualTo(4d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathBasic, "clamp", 7L, 1L, 5L), Is.EqualTo(5L));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathRounding, "floor", 2L), Is.EqualTo(2L));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathRounding, "ceiling", 2.1d), Is.EqualTo(3d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathRounding, "truncate", -2.9d), Is.EqualTo(-2d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathRounding, "round", 2.5d), Is.EqualTo(2d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathRounding, "truncateToInt", -2.9d), Is.EqualTo(-2L));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathPowers, "sqrt", 9L), Is.EqualTo(3d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathPowers, "pow", 2L, 3L), Is.EqualTo(8d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathPowers, "exp", 0L), Is.EqualTo(1d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathPowers, "log", 1L), Is.EqualTo(0d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathPowers, "log10", 100L), Is.EqualTo(2d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "sin", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "cos", 0L), Is.EqualTo(1d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "tan", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "asin", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "acos", 1L), Is.EqualTo(0d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "atan", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "atan2", 0L, 1L), Is.EqualTo(0d));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "degreesToRadians", 180L), Is.EqualTo(Math.PI));
                Assert.That(Invoke(DotNetStandardLibraryModules.MathTrigonometry, "radiansToDegrees", Math.PI), Is.EqualTo(180d));
            }
        );
    }

    [Test]
    public void MathClassificationHandlesIntegersNaNAndInfinity()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(Invoke(DotNetStandardLibraryModules.MathClassification, "isFinite", 1L), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.MathClassification, "isFinite", double.PositiveInfinity), Is.False);
                Assert.That(Invoke(DotNetStandardLibraryModules.MathClassification, "isInfinity", double.NegativeInfinity), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.MathClassification, "isNaN", double.NaN), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.MathPowers, "sqrt", -1L), Is.NaN);
            }
        );
    }

    [Test]
    public void MathFailuresUseStableApplicationErrors()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.MathBasic, "abs", long.MinValue).Code,
                    Is.EqualTo("mulang.std.overflow")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.MathBasic, "sign", double.NaN).Code,
                    Is.EqualTo("mulang.std.invalid_argument")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.MathBasic, "clamp", 1L, 2L, 1L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.MathRounding, "truncateToInt", double.PositiveInfinity).Code,
                    Is.EqualTo("mulang.std.conversion")
                );
            }
        );
    }

    [Test]
    public void StringFunctionsUseUnicodeScalarIndexes()
    {
        const string value = "A😀e\u0301";

        Assert.Multiple(
            static () =>
            {
                Assert.That(Invoke(DotNetStandardLibraryModules.StringInspection, "stringLength", value), Is.EqualTo(4L));
                Assert.That(Invoke(DotNetStandardLibraryModules.StringInspection, "charAt", value, 1L), Is.EqualTo(0x1F600L));
                Assert.That(Invoke(DotNetStandardLibraryModules.StringInspection, "isEmpty", ""), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.StringInspection, "isWhiteSpace", " \t"), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.StringInspection, "stringContains", value, "😀"), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.StringInspection, "startsWith", value, "A😀"), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.StringInspection, "endsWith", value, "e\u0301"), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.StringSearch, "indexOf", value, "e"), Is.EqualTo(2L));
                Assert.That(Invoke(DotNetStandardLibraryModules.StringSearch, "lastIndexOf", "😀a😀", "😀"), Is.EqualTo(2L));
                Assert.That(Invoke(DotNetStandardLibraryModules.StringSearch, "indexOf", value, "x"), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.StringTransform, "reverse", value), Is.EqualTo("\u0301e😀A"));
                Assert.That(Invoke(DotNetStandardLibraryModules.StringSlicing, "substring", value, 1L, 2L), Is.EqualTo("😀e"));
                Assert.That(Invoke(DotNetStandardLibraryModules.StringSlicing, "remove", value, 1L, 2L), Is.EqualTo("A\u0301"));
                Assert.That(Invoke(DotNetStandardLibraryModules.StringSlicing, "insert", value, 2L, "X"), Is.EqualTo("A😀Xe\u0301"));
            }
        );
    }

    [Test]
    public void StringTransformsAndComparisonsAreInvariantOrdinal()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Multiple(
                static () =>
                {
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringTransform, "toLower", "I"), Is.EqualTo("i"));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringTransform, "toUpper", "i"), Is.EqualTo("I"));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringTransform, "trim", " x "), Is.EqualTo("x"));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringTransform, "trimStart", " x "), Is.EqualTo("x "));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringTransform, "trimEnd", " x "), Is.EqualTo(" x"));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringTransform, "repeat", "ab", 3L), Is.EqualTo("ababab"));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringReplacement, "replaceFirst", "aaaa", "aa", "b"), Is.EqualTo("baa"));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringReplacement, "replaceAll", "aaaa", "aa", "b"), Is.EqualTo("bb"));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringComparison, "compareOrdinal", "a", "b"), Is.EqualTo(-1L));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringComparison, "compareIgnoreCase", "A", "a"), Is.EqualTo(0L));
                    Assert.That(Invoke(DotNetStandardLibraryModules.StringComparison, "equalsIgnoreCase", "A", "a"), Is.True);
                }
            );
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Test]
    public void StringRangeFailuresUseStableErrors()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.StringInspection, "charAt", "a", -1L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.StringSlicing, "substring", "a", 0L, 2L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.StringTransform, "repeat", "a", -1L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(DotNetStandardLibraryModules.StringReplacement, "replaceAll", "a", "", "b").Code,
                    Is.EqualTo("mulang.std.invalid_argument")
                );
            }
        );
    }

    [Test]
    public void ParsingUsesExactInvariantSyntax()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseInt", "9223372036854775807"), Is.EqualTo(long.MaxValue));
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseInt", "-0x8000000000000000"), Is.EqualTo(long.MinValue));
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseInt", " 1"), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseInt", "9223372036854775808"), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseFloat", "1.5e2"), Is.EqualTo(150d));
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseFloat", "nan"), Is.NaN);
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseFloat", "infty"), Is.EqualTo(double.PositiveInfinity));
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseFloat", "-infty"), Is.EqualTo(double.NegativeInfinity));
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseFloat", "+infty"), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseFloat", "1."), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseBool", "true"), Is.True);
                Assert.That(Invoke(DotNetStandardLibraryModules.Parsing, "parseBool", "True"), Is.Null);
            }
        );
    }

    [Test]
    public void Base64UsesStrictUtf8()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(Invoke(DotNetStandardLibraryModules.TextEncoding, "base64Encode", "é"), Is.EqualTo("w6k="));
                Assert.That(Invoke(DotNetStandardLibraryModules.TextEncoding, "base64Decode", "w6k="), Is.EqualTo("é"));
                Assert.That(Invoke(DotNetStandardLibraryModules.TextEncoding, "base64Decode", "not base64"), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.TextEncoding, "base64Decode", "w6k=\n"), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.TextEncoding, "base64Decode", "/w=="), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.TextEncoding, "base64Decode", "AB=="), Is.Null);
                Assert.That(Invoke(DotNetStandardLibraryModules.TextEncoding, "base64Decode", "AAB="), Is.Null);
            }
        );
    }

    [Test]
    public void ArraysUseStructuralEqualityAndSupportCycles()
    {
        MutableArray first = new ();
        MutableArray second = new ();
        first.Values.Add(first);
        second.Values.Add(second);
        MutableArray outer = new ();
        outer.Values.Add(first);

        Assert.That(
            Invoke(DotNetStandardLibraryModules.Array, "arrayContains", outer, second),
            Is.True
        );
    }

    [Test]
    public void ObjectFunctionsSortAdapterPropertiesAndReturnPublicReadOnlyArrays()
    {
        TestObject value = new (
            new Dictionary<string, object?>
            {
                ["z"] = 2L,
                ["a"] = null,
            }
        );

        IDotNetReadOnlyArrayValue keys = (IDotNetReadOnlyArrayValue)Invoke(
            DotNetStandardLibraryModules.Object,
            "objectKeys",
            value
        )!;
        IDotNetReadOnlyArrayValue values = (IDotNetReadOnlyArrayValue)Invoke(
            DotNetStandardLibraryModules.Object,
            "objectValues",
            value
        )!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(keys, Is.TypeOf<DotNetStandardLibraryReadOnlyArray>());
            Assert.That(keys.Count, Is.EqualTo(2));
            Assert.That(keys.TryGetElement(0, out object? firstKey), Is.True);
            Assert.That(firstKey, Is.EqualTo("a"));
            Assert.That(values.TryGetElement(0, out object? firstValue), Is.True);
            Assert.That(firstValue, Is.Null);
            Assert.That(values.TryGetElement(1, out object? secondValue), Is.True);
            Assert.That(secondValue, Is.EqualTo(2L));
        }
    }

    [Test]
    public void TraversalsObserveCancellation()
    {
        using CancellationTokenSource source = new ();
        source.Cancel();

        MuLangRuntimeException exception = Assert.Throws<MuLangRuntimeException>(
            () => StandardLibraryTestRuntime.Invoke(
                DotNetStandardLibraryModules.StringInspection,
                "stringLength",
                [ "value" ],
                // ReSharper disable once AccessToDisposedClosure
                source.Token
            )
        )!;

        Assert.That(exception.Error.Category, Is.EqualTo(RuntimeErrorCategory.Cancellation));
    }

    private static object? Invoke(
        DotNetStandardLibraryModuleBinding binding,
        string name,
        params object?[] arguments
    )
    {
        return StandardLibraryTestRuntime.Invoke(binding, name, arguments);
    }

    private sealed class MutableArray : IDotNetReadOnlyArrayValue
    {
        public object Identity => this;

        public int Count => Values.Count;

        public IList<object?> Values { get; } = new List<object?>();

        public bool TryGetElement(int index, out object? value)
        {
            if (index < 0 || index >= Values.Count)
            {
                value = null;
                return false;
            }

            value = Values[index];
            return true;
        }
    }

    private sealed class TestObject : IDotNetObjectValue
    {
        private readonly IDictionary<string, object?> values;

        public TestObject(IDictionary<string, object?> values)
        {
            this.values = values;
        }

        public object Identity => this;

        public IReadOnlyCollection<string> PropertyNames => [ .. values.Keys.Reverse() ];

        public bool TryGetProperty(string name, out object? value)
        {
            return values.TryGetValue(name, out value);
        }

        public bool TrySetProperty(string name, object? value)
        {
            values[name] = value;
            return true;
        }

        public bool TryRemoveProperty(string name)
        {
            return values.Remove(name);
        }
    }
}
