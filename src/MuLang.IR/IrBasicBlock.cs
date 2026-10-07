namespace MuLang.IR;

/// <summary>Represents a basic block in a MuLang IR function.</summary>
/// <param name="Id">The block identifier within the function.</param>
/// <param name="Instructions">The instructions executed by the block.</param>
/// <param name="Terminator">The control-flow terminator.</param>
public sealed record IrBasicBlock(
    int Id,
    IReadOnlyCollection<IrInstruction> Instructions,
    IrTerminator Terminator
)
{
    /// <summary>Initializes a new instance of the <see cref="IrBasicBlock" /> class with explicit lifetime ownership.</summary>
    /// <param name="id">The block identifier within the function.</param>
    /// <param name="instructions">The instructions executed by the block.</param>
    /// <param name="terminator">The control-flow terminator.</param>
    /// <param name="lifetimeRegion">The owning lifetime-region identifier.</param>
    public IrBasicBlock(
        int id,
        IReadOnlyCollection<IrInstruction> instructions,
        IrTerminator terminator,
        int lifetimeRegion
    )
        : this(id, instructions, terminator)
    {
        LifetimeRegion = lifetimeRegion;
    }

    /// <summary>Gets the identifier of the lifetime region that owns the block.</summary>
    public int LifetimeRegion { get; init; }
}
