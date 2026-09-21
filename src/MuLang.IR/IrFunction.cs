using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Represents an entry or user-defined function in portable MuLang IR.</summary>
/// <param name="Id">The function identifier.</param>
/// <param name="ReturnType">The declared return type.</param>
/// <param name="EntryBlock">The identifier of the entry block.</param>
/// <param name="Slots">The slots available to the function.</param>
/// <param name="Blocks">The function basic blocks.</param>
public sealed record IrFunction(
    string Id,
    TypeSymbol ReturnType,
    int EntryBlock,
    IReadOnlyList<IrSlot> Slots,
    IReadOnlyList<IrBasicBlock> Blocks
);
