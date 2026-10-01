using MuLang.Exporters.DotNet;

namespace MuLang.StandardLibrary.DotNet;

/// <summary>Represents a standard-library function implementation and its runtime contract.</summary>
public sealed class DotNetStandardLibraryFunction
{
    /// <summary>Initializes a new instance of the <see cref="DotNetStandardLibraryFunction" /> class.</summary>
    /// <param name="id">The declared provider identifier.</param>
    /// <param name="argumentCount">The required argument count.</param>
    /// <param name="implementation">The runtime implementation.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id" /> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="implementation" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="argumentCount" /> is negative.</exception>
    public DotNetStandardLibraryFunction(
        string id,
        int argumentCount,
        DotNetProviderFunction implementation
    )
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Function identifier cannot be null or whitespace.", nameof(id));
        }

        if (argumentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(argumentCount));
        }

        Id = id;
        ArgumentCount = argumentCount;
        Implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
    }

    /// <summary>Gets the declared provider identifier.</summary>
    public string Id { get; }

    /// <summary>Gets the required argument count.</summary>
    public int ArgumentCount { get; }

    /// <summary>Gets the runtime implementation.</summary>
    public DotNetProviderFunction Implementation { get; }
}
