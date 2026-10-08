namespace MuLang.IR;

/// <summary>Represents a basic block in a MuLang IR function.</summary>
/// <param name="Id">The block identifier within the function.</param>
/// <param name="Instructions">The instructions executed by the block.</param>
/// <param name="Terminator">The control-flow terminator.</param>
/// <param name="LifetimeRegion">The owning lifetime-region identifier.</param>
public sealed record IrBasicBlock(
    int Id,
    IReadOnlyCollection<IrInstruction> Instructions,
    IrTerminator Terminator,
    int LifetimeRegion
)
{
    /// <summary>Initializes a new instance of the <see cref="IrBasicBlock" /> class using root lifetime ownership.</summary>
    /// <param name="Id">The block identifier within the function.</param>
    /// <param name="Instructions">The instructions executed by the block.</param>
    /// <param name="Terminator">The control-flow terminator.</param>
    [Obsolete("Use the constructor that includes lifetime ownership.")]
    public IrBasicBlock(
        int Id,
        IReadOnlyCollection<IrInstruction> Instructions,
        IrTerminator Terminator
    )
        : this(Id, Instructions, Terminator, 0) { }

    /// <summary>Deconstructs the block using the legacy component shape.</summary>
    /// <param name="Id">The block identifier within the function.</param>
    /// <param name="Instructions">The instructions executed by the block.</param>
    /// <param name="Terminator">The control-flow terminator.</param>
    [Obsolete("Use the deconstruction shape that includes lifetime ownership.")]
    public void Deconstruct(
        out int Id,
        out IReadOnlyCollection<IrInstruction> Instructions,
        out IrTerminator Terminator
    )
    {
        Id = this.Id;
        Instructions = this.Instructions;
        Terminator = this.Terminator;
    }
}
