using MuLang.Core.Types;

namespace MuLang.IR;

internal sealed record IrFunction(
    string Id,
    TypeSymbol ReturnType,
    int EntryBlock,
    IReadOnlyList<IrSlot> Slots,
    IReadOnlyList<IrBasicBlock> Blocks
);
