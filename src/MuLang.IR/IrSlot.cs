using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Describes a value slot in a MuLang IR function.</summary>
/// <param name="Id">The slot identifier within the function.</param>
/// <param name="Kind">The slot kind.</param>
/// <param name="Type">The static slot type.</param>
/// <param name="Name">The source-level name, when available.</param>
public sealed record IrSlot(
    int Id,
    IrSlotKind Kind,
    TypeSymbol Type,
    string? Name
);
