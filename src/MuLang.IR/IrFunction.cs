using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Represents an entry or user-defined function in portable MuLang IR.</summary>
/// <param name="Id">The function identifier.</param>
/// <param name="ReturnType">The declared return type.</param>
/// <param name="EntryBlock">The identifier of the entry block.</param>
/// <param name="Slots">The slots available to the function.</param>
/// <param name="Blocks">The function basic blocks.</param>
/// <param name="LifetimeRegions">The function lifetime regions.</param>
/// <param name="ExceptionRegions">The function exception regions.</param>
public sealed record IrFunction(
    string Id,
    TypeSymbol ReturnType,
    int EntryBlock,
    IReadOnlyList<IrSlot> Slots,
    IReadOnlyList<IrBasicBlock> Blocks,
    IReadOnlyList<IrLifetimeRegion> LifetimeRegions,
    IReadOnlyList<IrExceptionRegion> ExceptionRegions
)
{
    /// <summary>Initializes a new instance of the <see cref="IrFunction" /> class using a root lifetime region.</summary>
    /// <param name="Id">The function identifier.</param>
    /// <param name="ReturnType">The declared return type.</param>
    /// <param name="EntryBlock">The identifier of the entry block.</param>
    /// <param name="Slots">The slots available to the function.</param>
    /// <param name="Blocks">The function basic blocks.</param>
    [Obsolete("Use the constructor that includes lifetime and exception regions.")]
    public IrFunction(
        string Id,
        TypeSymbol ReturnType,
        int EntryBlock,
        IReadOnlyList<IrSlot> Slots,
        IReadOnlyList<IrBasicBlock> Blocks
    )
        : this(
            Id,
            ReturnType,
            EntryBlock,
            Slots,
            Blocks,
            [ new IrLifetimeRegion(0, null, EntryBlock) ],
            [ ]
        ) { }

    /// <summary>Initializes a new instance of the <see cref="IrFunction" /> class without exception regions.</summary>
    /// <param name="id">The function identifier.</param>
    /// <param name="returnType">The declared return type.</param>
    /// <param name="entryBlock">The identifier of the entry block.</param>
    /// <param name="slots">The slots available to the function.</param>
    /// <param name="blocks">The function basic blocks.</param>
    /// <param name="lifetimeRegions">The function lifetime regions.</param>
    [Obsolete("Use the constructor that includes exception regions.")]
    public IrFunction(
        string id,
        TypeSymbol returnType,
        int entryBlock,
        IReadOnlyList<IrSlot> slots,
        IReadOnlyList<IrBasicBlock> blocks,
        IReadOnlyList<IrLifetimeRegion> lifetimeRegions
    )
        : this(id, returnType, entryBlock, slots, blocks, lifetimeRegions, [ ]) { }

    /// <summary>Deconstructs the function using the legacy component shape.</summary>
    /// <param name="Id">The function identifier.</param>
    /// <param name="ReturnType">The declared return type.</param>
    /// <param name="EntryBlock">The identifier of the entry block.</param>
    /// <param name="Slots">The slots available to the function.</param>
    /// <param name="Blocks">The function basic blocks.</param>
    [Obsolete("Use the deconstruction shape that includes lifetime and exception regions.")]
    public void Deconstruct(
        out string Id,
        out TypeSymbol ReturnType,
        out int EntryBlock,
        out IReadOnlyList<IrSlot> Slots,
        out IReadOnlyList<IrBasicBlock> Blocks
    )
    {
        Id = this.Id;
        ReturnType = this.ReturnType;
        EntryBlock = this.EntryBlock;
        Slots = this.Slots;
        Blocks = this.Blocks;
    }
}
