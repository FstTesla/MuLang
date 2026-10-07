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
)
{
    /// <summary>Initializes a new instance of the <see cref="IrFunction" /> class with explicit lifetime regions.</summary>
    /// <param name="id">The function identifier.</param>
    /// <param name="returnType">The declared return type.</param>
    /// <param name="entryBlock">The identifier of the entry block.</param>
    /// <param name="slots">The slots available to the function.</param>
    /// <param name="blocks">The function basic blocks.</param>
    /// <param name="lifetimeRegions">The function lifetime regions.</param>
    public IrFunction(
        string id,
        TypeSymbol returnType,
        int entryBlock,
        IReadOnlyList<IrSlot> slots,
        IReadOnlyList<IrBasicBlock> blocks,
        IReadOnlyList<IrLifetimeRegion> lifetimeRegions
    )
        : this(id, returnType, entryBlock, slots, blocks)
    {
        LifetimeRegions = lifetimeRegions;
    }

    /// <summary>Initializes a new instance of the <see cref="IrFunction" /> class with explicit lifetime and exception regions.</summary>
    /// <param name="id">The function identifier.</param>
    /// <param name="returnType">The declared return type.</param>
    /// <param name="entryBlock">The identifier of the entry block.</param>
    /// <param name="slots">The slots available to the function.</param>
    /// <param name="blocks">The function basic blocks.</param>
    /// <param name="lifetimeRegions">The function lifetime regions.</param>
    /// <param name="exceptionRegions">The function exception regions.</param>
    public IrFunction(
        string id,
        TypeSymbol returnType,
        int entryBlock,
        IReadOnlyList<IrSlot> slots,
        IReadOnlyList<IrBasicBlock> blocks,
        IReadOnlyList<IrLifetimeRegion> lifetimeRegions,
        IReadOnlyList<IrExceptionRegion> exceptionRegions
    )
        : this(id, returnType, entryBlock, slots, blocks, lifetimeRegions)
    {
        ExceptionRegions = exceptionRegions;
    }

    /// <summary>Gets the function lifetime regions.</summary>
    public IReadOnlyList<IrLifetimeRegion> LifetimeRegions { get; init; } =
    [
        new IrLifetimeRegion(0, null, EntryBlock),
    ];

    /// <summary>Gets the function exception regions.</summary>
    public IReadOnlyList<IrExceptionRegion> ExceptionRegions { get; init; } = [ ];
}
