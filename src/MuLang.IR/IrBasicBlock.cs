namespace MuLang.IR;

internal sealed record IrBasicBlock(
    int Id,
    IReadOnlyList<IrInstruction> Instructions,
    IrTerminator Terminator
);
