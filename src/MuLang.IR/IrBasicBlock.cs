namespace MuLang.IR;

internal sealed record IrBasicBlock(
    int Id,
    IReadOnlyCollection<IrInstruction> Instructions,
    IrTerminator Terminator
);
