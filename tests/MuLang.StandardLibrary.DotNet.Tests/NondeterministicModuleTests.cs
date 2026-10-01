using MuLang.Core.Runtime;
using MuLang.Exporters.DotNet;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class NondeterministicModuleTests
{
    [Test]
    public void RandomUsesInjectedSourceAndValidatesRanges()
    {
        DeterministicRandomSource source = new ();
        DotNetStandardLibrary standardLibrary = new (
            randomOptions: new RandomStandardLibraryOptions { RandomSource = source }
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(Invoke(standardLibrary, StandardLibraryCatalog.Functions.RandomFloat), Is.EqualTo(0.25d));
                Assert.That(Invoke(standardLibrary, StandardLibraryCatalog.Functions.RandomInt, 10L, 20L), Is.EqualTo(10L));
                Assert.That(source.LastMinimum, Is.EqualTo(10L));
                Assert.That(source.LastMaximum, Is.EqualTo(20L));
                Assert.That(
                    InvokeError(standardLibrary, StandardLibraryCatalog.Functions.RandomInt, 1L, 1L).Code,
                    Is.EqualTo("mulang.std.range")
                );
            }
        );
    }

    [Test]
    public void InvalidRandomSourceResultsBecomeProviderFailures()
    {
        DotNetStandardLibrary standardLibrary = new (
            randomOptions: new RandomStandardLibraryOptions
            {
                RandomSource = new InvalidRandomSource(),
            }
        );
        MuLangRuntimeException exception = InvokeError(
            standardLibrary,
            StandardLibraryCatalog.Functions.RandomFloat
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception.Code, Is.EqualTo(DotNetRuntimeErrorCodes.ProviderFailure));
            Assert.That(exception.Error.Category, Is.EqualTo(RuntimeErrorCategory.Provider));
            Assert.That(exception.Error.IsCatchable, Is.False);
        }
    }

    [Test]
    public void ClockUsesInjectedTimeProvider()
    {
        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);
        DotNetStandardLibrary standardLibrary = new (
            clockOptions: new ClockStandardLibraryOptions
            {
                TimeProvider = new FixedTimeProvider(timestamp),
            }
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    Invoke(standardLibrary, StandardLibraryCatalog.Functions.UnixTimeSeconds),
                    Is.EqualTo(timestamp.ToUnixTimeSeconds())
                );
                Assert.That(
                    Invoke(standardLibrary, StandardLibraryCatalog.Functions.UnixTimeMilliseconds),
                    Is.EqualTo(timestamp.ToUnixTimeMilliseconds())
                );
            }
        );
    }

    [Test]
    public void GuidFunctionsUseCanonicalFormattingVersionsAndInjectedTimestamp()
    {
        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);
        DotNetStandardLibrary standardLibrary = new (
            guidOptions: new GuidStandardLibraryOptions
            {
                TimeProvider = new FixedTimeProvider(timestamp),
            }
        );
        string version4 = (string)Invoke(standardLibrary, StandardLibraryCatalog.Functions.NewGuid)!;
        string version7 = (string)Invoke(standardLibrary, StandardLibraryCatalog.Functions.NewGuidV7)!;
        Guid parsed4 = Guid.ParseExact(version4, "D");
        Guid parsed7 = Guid.ParseExact(version7, "D");
        byte[] bytes = parsed7.ToByteArray(true);
        long encodedMilliseconds =
            ((long)bytes[0] << 40) |
            ((long)bytes[1] << 32) |
            ((long)bytes[2] << 24) |
            ((long)bytes[3] << 16) |
            ((long)bytes[4] << 8) |
            bytes[5];

        Assert.Multiple(
            () =>
            {
                Assert.That(parsed4.Version, Is.EqualTo(4));
                Assert.That(parsed7.Version, Is.EqualTo(7));
                Assert.That(version4, Is.EqualTo(version4.ToLowerInvariant()));
                Assert.That(version7, Is.EqualTo(version7.ToLowerInvariant()));
                Assert.That(encodedMilliseconds, Is.EqualTo(timestamp.ToUnixTimeMilliseconds()));
                Assert.That(Invoke(standardLibrary, StandardLibraryCatalog.Functions.IsGuid, version7.ToUpperInvariant()), Is.True);
                Assert.That(Invoke(standardLibrary, StandardLibraryCatalog.Functions.IsGuid, parsed7.ToString("N")), Is.False);
                Assert.That(Invoke(standardLibrary, StandardLibraryCatalog.Functions.IsGuid, "not-a-guid"), Is.False);
            }
        );
    }

    [Test]
    public void GuidFunctionsRequestOnlyRequiredConfiguration()
    {
        DotNetStandardLibrary standardLibrary = new (
            clockOptions: new ClockStandardLibraryOptions { TimeProvider = null! },
            guidOptions: new GuidStandardLibraryOptions { TimeProvider = null! }
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(
                    Invoke(standardLibrary, StandardLibraryCatalog.Functions.IsGuid, Guid.NewGuid().ToString("D")),
                    Is.True
                );
                Assert.That(
                    Invoke(standardLibrary, StandardLibraryCatalog.Functions.NewGuid),
                    Is.TypeOf<string>()
                );
                Assert.That(
                    () => standardLibrary.Bind(
                        StandardLibrarySelection.Create(
                            [ StandardLibraryCatalog.Functions.NewGuidV7 ]
                        )
                    ),
                    Throws.InvalidOperationException.With.Message.Contains("newGuidV7")
                );
                Assert.That(
                    () => standardLibrary.Bind(
                        StandardLibrarySelection.Create(
                            [ StandardLibraryCatalog.Functions.UnixTimeSeconds ]
                        )
                    ),
                    Throws.InvalidOperationException.With.Message.Contains("Clock")
                );
            }
        );
    }

    [Test]
    public void ConstructorCapturesConfiguredServices()
    {
        DateTimeOffset firstTimestamp = DateTimeOffset.FromUnixTimeSeconds(100);
        DateTimeOffset secondTimestamp = DateTimeOffset.FromUnixTimeSeconds(200);
        RandomStandardLibraryOptions randomOptions = new ()
        {
            RandomSource = new DeterministicRandomSource(0.25d),
        };
        ClockStandardLibraryOptions clockOptions = new ()
        {
            TimeProvider = new FixedTimeProvider(firstTimestamp),
        };
        GuidStandardLibraryOptions guidOptions = new ()
        {
            TimeProvider = new FixedTimeProvider(firstTimestamp),
        };
        DotNetStandardLibrary standardLibrary = new (
            randomOptions,
            clockOptions,
            guidOptions
        );

        randomOptions.RandomSource = new DeterministicRandomSource(0.75d);
        clockOptions.TimeProvider = new FixedTimeProvider(secondTimestamp);
        guidOptions.TimeProvider = new FixedTimeProvider(secondTimestamp);

        string version7 = (string)Invoke(
            standardLibrary,
            StandardLibraryCatalog.Functions.NewGuidV7
        )!;
        byte[] bytes = Guid.ParseExact(version7, "D").ToByteArray(true);
        long encodedMilliseconds =
            ((long)bytes[0] << 40) |
            ((long)bytes[1] << 32) |
            ((long)bytes[2] << 24) |
            ((long)bytes[3] << 16) |
            ((long)bytes[4] << 8) |
            bytes[5];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                Invoke(standardLibrary, StandardLibraryCatalog.Functions.RandomFloat),
                Is.EqualTo(0.25d)
            );
            Assert.That(
                Invoke(standardLibrary, StandardLibraryCatalog.Functions.UnixTimeSeconds),
                Is.EqualTo(firstTimestamp.ToUnixTimeSeconds())
            );
            Assert.That(encodedMilliseconds, Is.EqualTo(firstTimestamp.ToUnixTimeMilliseconds()));
        }
    }

    private static object? Invoke(
        DotNetStandardLibrary standardLibrary,
        StandardLibraryFunction function,
        params object?[] arguments
    )
    {
        StandardLibraryModule module = StandardLibraryCatalog.Modules.All.Single(
            candidate => candidate.Functions.Contains(function)
        );
        return StandardLibraryTestRuntime.Invoke(
            module,
            function.Name,
            arguments,
            standardLibrary: standardLibrary
        );
    }

    private static MuLangRuntimeException InvokeError(
        DotNetStandardLibrary standardLibrary,
        StandardLibraryFunction function,
        params object?[] arguments
    )
    {
        return Assert.Throws<MuLangRuntimeException>(
            () => Invoke(standardLibrary, function, arguments)
        )!;
    }

    private sealed class DeterministicRandomSource : IStandardLibraryRandomSource
    {
        private readonly double value;

        public DeterministicRandomSource(double value = 0.25d)
        {
            this.value = value;
        }

        public long LastMaximum { get; private set; }

        public long LastMinimum { get; private set; }

        public double NextDouble()
        {
            return value;
        }

        public long NextInt64(long minimum, long maximum)
        {
            LastMinimum = minimum;
            LastMaximum = maximum;
            return minimum;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset timestamp;

        public FixedTimeProvider(DateTimeOffset timestamp)
        {
            this.timestamp = timestamp;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return timestamp;
        }
    }

    private sealed class InvalidRandomSource : IStandardLibraryRandomSource
    {
        public double NextDouble()
        {
            return 1d;
        }

        public long NextInt64(long minimum, long maximum)
        {
            return maximum;
        }
    }
}
