using MuLang.Core.Types;

namespace MuLang.IR;

internal sealed record IrSlot(
    int Id,
    IrSlotKind Kind,
    TypeSymbol Type,
    string? Name
);
