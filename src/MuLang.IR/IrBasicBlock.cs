namespace MuLang.IR;

/// <summary>Represents a basic block in a MuLang IR function.</summary>
/// <param name="Id">The block identifier within the function.</param>
/// <param name="Instructions">The instructions executed by the block.</param>
/// <param name="Terminator">The control-flow terminator.</param>
public sealed record IrBasicBlock(
    int Id,
    IReadOnlyCollection<IrInstruction> Instructions,
    IrTerminator Terminator
);
