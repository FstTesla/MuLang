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
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathBasic, "abs", -4L), Is.EqualTo(4L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathBasic, "abs", -4.5d), Is.EqualTo(4.5d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathBasic, "sign", -0.5d), Is.EqualTo(-1L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathBasic, "min", 4L, 2L), Is.EqualTo(2L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathBasic, "max", 4L, 2.5d), Is.EqualTo(4d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathBasic, "clamp", 7L, 1L, 5L), Is.EqualTo(5L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathRounding, "floor", 2L), Is.EqualTo(2L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathRounding, "ceiling", 2.1d), Is.EqualTo(3d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathRounding, "truncate", -2.9d), Is.EqualTo(-2d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathRounding, "round", 2.5d), Is.EqualTo(2d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathRounding, "truncateToInt", -2.9d), Is.EqualTo(-2L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathPowers, "sqrt", 9L), Is.EqualTo(3d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathPowers, "pow", 2L, 3L), Is.EqualTo(8d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathPowers, "exp", 0L), Is.EqualTo(1d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathPowers, "log", 1L), Is.EqualTo(0d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathPowers, "log10", 100L), Is.EqualTo(2d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "sin", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "cos", 0L), Is.EqualTo(1d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "tan", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "asin", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "acos", 1L), Is.EqualTo(0d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "atan", 0L), Is.EqualTo(0d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "atan2", 0L, 1L), Is.EqualTo(0d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "degreesToRadians", 180L), Is.EqualTo(Math.PI));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathTrigonometry, "radiansToDegrees", Math.PI), Is.EqualTo(180d));
            }
        );
    }

    [Test]
    public void MathClassificationHandlesIntegersNaNAndInfinity()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathClassification, "isFinite", 1L), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathClassification, "isFinite", double.PositiveInfinity), Is.False);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathClassification, "isInfinity", double.NegativeInfinity), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathClassification, "isNaN", double.NaN), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.MathPowers, "sqrt", -1L), Is.NaN);
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
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.MathBasic, "abs", long.MinValue).Code,
                    Is.EqualTo("mulang.std.overflow")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.MathBasic, "sign", double.NaN).Code,
                    Is.EqualTo("mulang.std.invalid_argument")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.MathBasic, "clamp", 1L, 2L, 1L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.MathRounding, "truncateToInt", double.PositiveInfinity).Code,
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
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringInspection, "stringLength", value), Is.EqualTo(4L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringInspection, "charAt", value, 1L), Is.EqualTo(0x1F600L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringInspection, "isEmpty", ""), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringInspection, "isWhiteSpace", " \t"), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringInspection, "stringContains", value, "😀"), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringInspection, "startsWith", value, "A😀"), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringInspection, "endsWith", value, "e\u0301"), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringSearch, "indexOf", value, "e"), Is.EqualTo(2L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringSearch, "lastIndexOf", "😀a😀", "😀"), Is.EqualTo(2L));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringSearch, "indexOf", value, "x"), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringTransform, "reverse", value), Is.EqualTo("\u0301e😀A"));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringSlicing, "substring", value, 1L, 2L), Is.EqualTo("😀e"));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringSlicing, "remove", value, 1L, 2L), Is.EqualTo("A\u0301"));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.StringSlicing, "insert", value, 2L, "X"), Is.EqualTo("A😀Xe\u0301"));
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
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringTransform, "toLower", "I"), Is.EqualTo("i"));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringTransform, "toUpper", "i"), Is.EqualTo("I"));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringTransform, "trim", " x "), Is.EqualTo("x"));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringTransform, "trimStart", " x "), Is.EqualTo("x "));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringTransform, "trimEnd", " x "), Is.EqualTo(" x"));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringTransform, "repeat", "ab", 3L), Is.EqualTo("ababab"));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringReplacement, "replaceFirst", "aaaa", "aa", "b"), Is.EqualTo("baa"));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringReplacement, "replaceAll", "aaaa", "aa", "b"), Is.EqualTo("bb"));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringComparison, "compareOrdinal", "a", "b"), Is.EqualTo(-1L));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringComparison, "compareIgnoreCase", "A", "a"), Is.EqualTo(0L));
                    Assert.That(Invoke(StandardLibraryCatalog.Modules.StringComparison, "equalsIgnoreCase", "A", "a"), Is.True);
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
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.StringInspection, "charAt", "a", -1L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.StringSlicing, "substring", "a", 0L, 2L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.StringTransform, "repeat", "a", -1L).Code,
                    Is.EqualTo("mulang.std.range")
                );
                Assert.That(
                    StandardLibraryTestRuntime.InvokeError(StandardLibraryCatalog.Modules.StringReplacement, "replaceAll", "a", "", "b").Code,
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
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseInt", "9223372036854775807"), Is.EqualTo(long.MaxValue));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseInt", "-0x8000000000000000"), Is.EqualTo(long.MinValue));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseInt", " 1"), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseInt", "9223372036854775808"), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseFloat", "1.5e2"), Is.EqualTo(150d));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseFloat", "nan"), Is.NaN);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseFloat", "infty"), Is.EqualTo(double.PositiveInfinity));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseFloat", "-infty"), Is.EqualTo(double.NegativeInfinity));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseFloat", "+infty"), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseFloat", "1."), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseBool", "true"), Is.True);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.Parsing, "parseBool", "True"), Is.Null);
            }
        );
    }

    [Test]
    public void Base64UsesStrictUtf8()
    {
        Assert.Multiple(
            static () =>
            {
                Assert.That(Invoke(StandardLibraryCatalog.Modules.TextEncoding, "base64Encode", "é"), Is.EqualTo("w6k="));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.TextEncoding, "base64Decode", "w6k="), Is.EqualTo("é"));
                Assert.That(Invoke(StandardLibraryCatalog.Modules.TextEncoding, "base64Decode", "not base64"), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.TextEncoding, "base64Decode", "w6k=\n"), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.TextEncoding, "base64Decode", "/w=="), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.TextEncoding, "base64Decode", "AB=="), Is.Null);
                Assert.That(Invoke(StandardLibraryCatalog.Modules.TextEncoding, "base64Decode", "AAB="), Is.Null);
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
            Invoke(StandardLibraryCatalog.Modules.Array, "arrayContains", outer, second),
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
            StandardLibraryCatalog.Modules.Object,
            "objectKeys",
            value
        )!;
        IDotNetReadOnlyArrayValue values = (IDotNetReadOnlyArrayValue)Invoke(
            StandardLibraryCatalog.Modules.Object,
            "objectValues",
            value
        )!;

        using (Assert.EnterMultipleScope())
        {
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
                StandardLibraryCatalog.Modules.StringInspection,
                "stringLength",
                [ "value" ],
                // ReSharper disable once AccessToDisposedClosure
                source.Token
            )
        )!;

        Assert.That(exception.Error.Category, Is.EqualTo(RuntimeErrorCategory.Cancellation));
    }

    private static object? Invoke(
        StandardLibraryModule module,
        string name,
        params object?[] arguments
    )
    {
        return StandardLibraryTestRuntime.Invoke(module, name, arguments);
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
