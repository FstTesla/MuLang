namespace MuLang.IR.Serialization;

/// <summary>Represents a grammatical identifier occurrence in a parsed MuIR document.</summary>
/// <param name="Kind">The identifier kind.</param>
/// <param name="Id">The decoded identifier, normalized for numeric references.</param>
/// <param name="FunctionId">The containing function for function-local identifiers.</param>
/// <param name="Role">The operand or declaration role.</param>
/// <param name="SelectionRange">The identifier token range in the MuIR document.</param>
/// <param name="Range">The complete declaration range, or the token range for references.</param>
public sealed record MuIrOccurrence(
    MuIrSymbolKind Kind,
    string Id,
    string? FunctionId,
    MuIrOccurrenceRole Role,
    MuIrRange SelectionRange,
    MuIrRange Range
);
