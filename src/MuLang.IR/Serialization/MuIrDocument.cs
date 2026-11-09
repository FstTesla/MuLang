using MuLang.Core.Types;

namespace MuLang.IR.Serialization;

/// <summary>Represents parsed MuIR document locations and wire identities, independently of executable IR source spans.</summary>
public sealed class MuIrDocument
{
    internal MuIrDocument(
        int formatVersion,
        IReadOnlyList<TypeSymbol> types,
        IReadOnlyList<MuIrToken> tokens,
        IReadOnlyList<MuIrOccurrence> occurrences,
        IReadOnlyList<MuIrRange> structuralRanges
    )
    {
        FormatVersion = formatVersion;
        Types = Array.AsReadOnly([ .. types ]);
        Tokens = Array.AsReadOnly([ .. tokens ]);
        Occurrences = Array.AsReadOnly([ .. occurrences ]);
        StructuralRanges = Array.AsReadOnly([ .. structuralRanges ]);
    }

    /// <summary>Gets the supported wire format version.</summary>
    public int FormatVersion { get; }

    /// <summary>Gets all declared wire types in ID order, including unused and structurally equivalent declarations.</summary>
    public IReadOnlyList<TypeSymbol> Types { get; }

    /// <summary>Gets the decoded tokens with their original document ranges.</summary>
    public IReadOnlyList<MuIrToken> Tokens { get; }

    /// <summary>Gets the grammatical declarations and operand occurrences.</summary>
    public IReadOnlyList<MuIrOccurrence> Occurrences { get; }

    /// <summary>Gets the complete delimited structural ranges.</summary>
    public IReadOnlyList<MuIrRange> StructuralRanges { get; }
}
