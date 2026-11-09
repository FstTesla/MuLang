namespace MuLang.IR.Serialization;

/// <summary>Represents a decoded token and its original MuIR document range.</summary>
/// <param name="Value">The decoded token value.</param>
/// <param name="Range">The original token range.</param>
/// <param name="IsString">Whether the token is a quoted string.</param>
public sealed record MuIrToken(string Value, MuIrRange Range, bool IsString);
