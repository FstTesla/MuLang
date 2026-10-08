using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal sealed class IrBuilder
{
    private readonly IList<IrSlot> slots = [ ];
    private readonly IList<MutableIrBlock> blocks = [ ];
    private readonly IList<IrLifetimeRegion> lifetimeRegions = [ ];
    private readonly IList<MutableIrExceptionRegion> exceptionRegions = [ ];
    private readonly Stack<int> lifetimeRegionStack = [ ];
    private readonly Stack<ExceptionRegionContext> exceptionRegionStack = [ ];

    public int EntryBlock { get; }

    public MutableIrBlock? CurrentBlock { get; private set; }

    public bool IsCurrentTerminated => CurrentBlock?.Terminator is not null;

    public IrBuilder()
    {
        lifetimeRegionStack.Push(0);
        EntryBlock = CreateBlock();
        lifetimeRegions.Add(new IrLifetimeRegion(0, null, EntryBlock));
        CurrentBlock = GetBlock(EntryBlock);
    }

    public int CreateBlock()
    {
        int id = blocks.Count;
        blocks.Add(new MutableIrBlock(id, lifetimeRegionStack.Peek()));

        if (
            exceptionRegionStack.TryPeek(
                out ExceptionRegionContext? context
            )
        )
        {
            exceptionRegions[context.RegionId].AddBlock(context.Part, id);
        }

        return id;
    }

    public int BeginExceptionRegion(int protectedEntry)
    {
        ExceptionRegionContext? parent = exceptionRegionStack.TryPeek(
            out ExceptionRegionContext? context
        )
            ? context
            : null;
        int id = exceptionRegions.Count;
        MutableIrExceptionRegion region = new (
            id,
            parent?.RegionId,
            parent?.Part,
            protectedEntry
        );
        exceptionRegions.Add(region);
        exceptionRegionStack.Push(
            new ExceptionRegionContext(id, IrExceptionRegionPart.Protected)
        );
        region.AddBlock(IrExceptionRegionPart.Protected, protectedEntry);
        return id;
    }

    public void BeginExceptionHandler(int entryBlock, int errorSlot)
    {
        ExceptionRegionContext context = GetExceptionRegionContext(
            IrExceptionRegionPart.Protected
        );
        exceptionRegionStack.Pop();
        exceptionRegionStack.Push(
            context with { Part = IrExceptionRegionPart.Handler }
        );
        MutableIrExceptionRegion region = exceptionRegions[context.RegionId];
        region.HandlerEntry = entryBlock;
        region.HandlerErrorSlot = errorSlot;
        region.AddBlock(IrExceptionRegionPart.Handler, entryBlock);
    }

    public void BeginExceptionCleanup(int entryBlock)
    {
        if (
            !exceptionRegionStack.TryPeek(
                out ExceptionRegionContext? context
            )
        )
        {
            throw new InvalidOperationException("No exception region is active.");
        }

        exceptionRegionStack.Pop();
        exceptionRegionStack.Push(
            context with { Part = IrExceptionRegionPart.Cleanup }
        );
        MutableIrExceptionRegion region = exceptionRegions[context.RegionId];
        region.CleanupEntry = entryBlock;
        region.AddBlock(IrExceptionRegionPart.Cleanup, entryBlock);
    }

    public void EndExceptionRegion()
    {
        if (!exceptionRegionStack.TryPop(out _))
        {
            throw new InvalidOperationException("No exception region is active.");
        }
    }

    public void EnterLifetimeRegion(TextSpan span)
    {
        MutableIrBlock parentBlock = CurrentBlock ??
            throw new InvalidOperationException("Cannot enter a lifetime region from an unreachable block.");
        int parentRegion = lifetimeRegionStack.Peek();
        int regionId = lifetimeRegions.Count;
        lifetimeRegionStack.Push(regionId);
        int entryBlock = CreateBlock();
        lifetimeRegions.Add(
            new IrLifetimeRegion(regionId, parentRegion, entryBlock)
        );
        CurrentBlock = parentBlock;
        Terminate(new IrTerminator.Jump(span, entryBlock));
        SwitchTo(entryBlock);
    }

    public void ExitLifetimeRegion(TextSpan span)
    {
        if (lifetimeRegionStack.Count == 1)
        {
            throw new InvalidOperationException("Cannot exit the root lifetime region.");
        }

        MutableIrBlock? regionEnd = CurrentBlock;
        lifetimeRegionStack.Pop();

        if (regionEnd is null)
        {
            return;
        }

        int continuation = CreateBlock();
        CurrentBlock = regionEnd;
        Terminate(new IrTerminator.Jump(span, continuation));
        SwitchTo(continuation);
    }

    public void SwitchTo(int blockId)
    {
        CurrentBlock = GetBlock(blockId);
    }

    public void SetUnreachable()
    {
        CurrentBlock = null;
    }

    public int CreateSlot(
        IrSlotKind kind,
        TypeSymbol type,
        string? name = null,
        IrSlotMutability? mutability = null
    )
    {
        int id = slots.Count;
        IrSlotMutability effectiveMutability = mutability ??
            (
                kind == IrSlotKind.Parameter
                    ? IrSlotMutability.ReadOnly
                    : IrSlotMutability.Mutable
            );
        slots.Add(
            new IrSlot(
                id,
                kind,
                type,
                name,
                effectiveMutability,
                lifetimeRegionStack.Peek()
            )
        );

        return id;
    }

    public void Emit(IrInstruction instruction)
    {
        MutableIrBlock block = CurrentBlock ??
            throw new InvalidOperationException("Cannot emit into an unreachable block.");

        if (block.Terminator is not null)
        {
            throw new InvalidOperationException($"IR block {block.Id} is already terminated.");
        }

        block.Add(instruction);
    }

    public void Terminate(IrTerminator terminator)
    {
        MutableIrBlock block = CurrentBlock ??
            throw new InvalidOperationException("Cannot terminate an unreachable block.");

        if (block.Terminator is not null)
        {
            throw new InvalidOperationException($"IR block {block.Id} is already terminated.");
        }

        block.Terminator = terminator;
    }

    public IrFunction Build(
        string id,
        TypeSymbol resultType
    )
    {
        if (lifetimeRegionStack.Count != 1)
        {
            throw new InvalidOperationException(
                "Cannot build while a nested lifetime region is active."
            );
        }

        if (exceptionRegionStack.Count != 0)
        {
            throw new InvalidOperationException(
                "Cannot build while an exception region is active."
            );
        }

        IReadOnlyList<IrBasicBlock> immutableBlocks = [ .. blocks.Select(static block => block.Build()) ];

        return new IrFunction(
            id,
            resultType,
            EntryBlock,
            slots.AsReadOnly(),
            immutableBlocks,
            lifetimeRegions.AsReadOnly(),
            [
                .. exceptionRegions.Select(
                    static region => region.Build()
                ),
            ]
        );
    }

    private MutableIrBlock GetBlock(int id)
    {
        if (id < 0 || id >= blocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        return blocks[id];
    }

    private ExceptionRegionContext GetExceptionRegionContext(
        IrExceptionRegionPart expectedPart
    )
    {
        if (
            !exceptionRegionStack.TryPeek(out ExceptionRegionContext? context) ||
            context.Part != expectedPart
        )
        {
            throw new InvalidOperationException(
                $"Expected an active {expectedPart.ToString().ToLowerInvariant()} exception component."
            );
        }

        return context;
    }

    private sealed record ExceptionRegionContext(
        int RegionId,
        IrExceptionRegionPart Part
    );
}
