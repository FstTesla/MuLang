namespace MuLang.IR.Serialization;

/// <summary>Represents a range in the MuIR document, measured in UTF-16 code units rather than original-source scalar offsets.</summary>
/// <param name="Offset">The zero-based document offset.</param>
/// <param name="Length">The range length.</param>
public readonly record struct MuIrRange(int Offset, int Length);
