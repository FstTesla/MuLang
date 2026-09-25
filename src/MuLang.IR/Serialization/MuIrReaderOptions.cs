namespace MuLang.IR.Serialization;

/// <summary>Represents resource limits applied while reading MuIR.</summary>
public sealed class MuIrReaderOptions
{
    /// <summary>Initializes a new instance of the <see cref="MuIrReaderOptions" /> class.</summary>
    /// <param name="maximumDocumentLength">The maximum document length in characters.</param>
    /// <param name="maximumStringLength">The maximum decoded string length.</param>
    /// <param name="maximumTokenLength">The maximum lexical token length.</param>
    /// <param name="maximumTypeNestingDepth">The maximum composite-type nesting depth.</param>
    /// <param name="maximumTypes">The maximum number of type definitions.</param>
    /// <param name="maximumFunctions">The maximum number of functions.</param>
    /// <param name="maximumSlotsPerFunction">The maximum number of slots in one function.</param>
    /// <param name="maximumBlocksPerFunction">The maximum number of blocks in one function.</param>
    /// <param name="maximumInstructionsPerBlock">The maximum number of instructions in one block.</param>
    /// <param name="maximumListElements">The maximum number of elements in one list operand.</param>
    /// <param name="maximumDiagnostics">The maximum number of diagnostics.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a limit is not positive.</exception>
    public MuIrReaderOptions(
        int maximumDocumentLength = 16 * 1024 * 1024,
        int maximumStringLength = 1024 * 1024,
        int maximumTokenLength = 1024 * 1024,
        int maximumTypeNestingDepth = 64,
        int maximumTypes = 10_000,
        int maximumFunctions = 1_000,
        int maximumSlotsPerFunction = 100_000,
        int maximumBlocksPerFunction = 100_000,
        int maximumInstructionsPerBlock = 100_000,
        int maximumListElements = 100_000,
        int maximumDiagnostics = 100
    )
    {
        MaximumDocumentLength = Validate(maximumDocumentLength, nameof(maximumDocumentLength));
        MaximumStringLength = Validate(maximumStringLength, nameof(maximumStringLength));
        MaximumTokenLength = Validate(maximumTokenLength, nameof(maximumTokenLength));
        MaximumTypeNestingDepth = Validate(maximumTypeNestingDepth, nameof(maximumTypeNestingDepth));
        MaximumTypes = Validate(maximumTypes, nameof(maximumTypes));
        MaximumFunctions = Validate(maximumFunctions, nameof(maximumFunctions));
        MaximumSlotsPerFunction = Validate(maximumSlotsPerFunction, nameof(maximumSlotsPerFunction));
        MaximumBlocksPerFunction = Validate(maximumBlocksPerFunction, nameof(maximumBlocksPerFunction));
        MaximumInstructionsPerBlock = Validate(
            maximumInstructionsPerBlock,
            nameof(maximumInstructionsPerBlock)
        );
        MaximumListElements = Validate(maximumListElements, nameof(maximumListElements));
        MaximumDiagnostics = Validate(maximumDiagnostics, nameof(maximumDiagnostics));
    }

    /// <summary>Gets the default reader options.</summary>
    public static MuIrReaderOptions Default { get; } = new ();

    /// <summary>Gets the maximum document length in characters.</summary>
    public int MaximumDocumentLength { get; }

    /// <summary>Gets the maximum decoded string length.</summary>
    public int MaximumStringLength { get; }

    /// <summary>Gets the maximum lexical token length.</summary>
    public int MaximumTokenLength { get; }

    /// <summary>Gets the maximum composite-type nesting depth.</summary>
    public int MaximumTypeNestingDepth { get; }

    /// <summary>Gets the maximum number of type definitions.</summary>
    public int MaximumTypes { get; }

    /// <summary>Gets the maximum number of functions.</summary>
    public int MaximumFunctions { get; }

    /// <summary>Gets the maximum number of slots in one function.</summary>
    public int MaximumSlotsPerFunction { get; }

    /// <summary>Gets the maximum number of blocks in one function.</summary>
    public int MaximumBlocksPerFunction { get; }

    /// <summary>Gets the maximum number of instructions in one block.</summary>
    public int MaximumInstructionsPerBlock { get; }

    /// <summary>Gets the maximum number of elements in one list operand.</summary>
    public int MaximumListElements { get; }

    /// <summary>Gets the maximum number of diagnostics.</summary>
    public int MaximumDiagnostics { get; }

    private static int Validate(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }
}
