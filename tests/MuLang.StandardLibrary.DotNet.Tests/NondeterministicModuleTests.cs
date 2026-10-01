using MuLang.Exporters.DotNet;

namespace MuLang.StandardLibrary.DotNet.Tests;

public sealed class NondeterministicModuleTests
{
    [Test]
    public void RandomUsesInjectedSourceAndValidatesRanges()
    {
        DeterministicRandomSource source = new ();
        DotNetStandardLibraryModuleBinding binding = DotNetStandardLibraryModules.CreateRandom(
            new RandomStandardLibraryOptions { RandomSource = source }
        );

        Assert.Multiple(() =>
        {
            Assert.That(StandardLibraryTestRuntime.Invoke(binding, "randomFloat", [ ]), Is.EqualTo(0.25d));
            Assert.That(StandardLibraryTestRuntime.Invoke(binding, "randomInt", [ 10L, 20L ]), Is.EqualTo(10L));
            Assert.That(source.LastMinimum, Is.EqualTo(10L));
            Assert.That(source.LastMaximum, Is.EqualTo(20L));
            Assert.That(
                StandardLibraryTestRuntime.InvokeError(binding, "randomInt", 1L, 1L).Code,
                Is.EqualTo("mulang.std.range")
            );
        });
    }

    [Test]
    public void ClockUsesInjectedTimeProvider()
    {
        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);
        DotNetStandardLibraryModuleBinding binding = DotNetStandardLibraryModules.CreateClock(
            new ClockStandardLibraryOptions { TimeProvider = new FixedTimeProvider(timestamp) }
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                StandardLibraryTestRuntime.Invoke(binding, "unixTimeSeconds", [ ]),
                Is.EqualTo(timestamp.ToUnixTimeSeconds())
            );
            Assert.That(
                StandardLibraryTestRuntime.Invoke(binding, "unixTimeMilliseconds", [ ]),
                Is.EqualTo(timestamp.ToUnixTimeMilliseconds())
            );
        });
    }

    [Test]
    public void GuidFunctionsUseCanonicalFormattingVersionsAndInjectedTimestamp()
    {
        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);
        DotNetStandardLibraryModuleBinding binding = DotNetStandardLibraryModules.CreateGuid(
            new GuidStandardLibraryOptions { TimeProvider = new FixedTimeProvider(timestamp) }
        );
        string version4 = (string)StandardLibraryTestRuntime.Invoke(binding, "newGuid", [ ])!;
        string version7 = (string)StandardLibraryTestRuntime.Invoke(binding, "newGuidV7", [ ])!;
        System.Guid parsed4 = System.Guid.ParseExact(version4, "D");
        System.Guid parsed7 = System.Guid.ParseExact(version7, "D");
        long encodedMilliseconds =
            ((long)parsed7.ToByteArray(true)[0] << 40) |
            ((long)parsed7.ToByteArray(true)[1] << 32) |
            ((long)parsed7.ToByteArray(true)[2] << 24) |
            ((long)parsed7.ToByteArray(true)[3] << 16) |
            ((long)parsed7.ToByteArray(true)[4] << 8) |
            parsed7.ToByteArray(true)[5];

        Assert.Multiple(() =>
        {
            Assert.That(parsed4.Version, Is.EqualTo(4));
            Assert.That(parsed7.Version, Is.EqualTo(7));
            Assert.That(version4, Is.EqualTo(version4.ToLowerInvariant()));
            Assert.That(version7, Is.EqualTo(version7.ToLowerInvariant()));
            Assert.That(encodedMilliseconds, Is.EqualTo(timestamp.ToUnixTimeMilliseconds()));
            Assert.That(StandardLibraryTestRuntime.Invoke(binding, "isGuid", [ version7.ToUpperInvariant() ]), Is.True);
            Assert.That(StandardLibraryTestRuntime.Invoke(binding, "isGuid", [ parsed7.ToString("N") ]), Is.False);
            Assert.That(StandardLibraryTestRuntime.Invoke(binding, "isGuid", [ "not-a-guid" ]), Is.False);
        });
    }

    private sealed class DeterministicRandomSource : IStandardLibraryRandomSource
    {
        public long LastMaximum { get; private set; }

        public long LastMinimum { get; private set; }

        public double NextDouble()
        {
            return 0.25d;
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
}
