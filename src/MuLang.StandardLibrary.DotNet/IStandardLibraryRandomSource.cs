namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents an interface for standard-library random value generation.</summary>
public interface IStandardLibraryRandomSource
{
    /// <summary>Returns a floating-point value in the range from zero inclusive to one exclusive.</summary>
    /// <returns>The generated value.</returns>
    double NextDouble();

    /// <summary>Returns an integer in the specified half-open range.</summary>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="maximum">The exclusive upper bound.</param>
    /// <returns>The generated value.</returns>
    long NextInt64(long minimum, long maximum);
}
