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

    private readonly IDictionary<int, ExceptionRegionContext> blockExceptionOwners =
        new Dictionary<int, ExceptionRegionContext>();

    public int EntryBlock { get; }

    public MutableIrBlock? CurrentBlock { get; private set; }

    public bool IsCurrentTerminated => CurrentBlock?.Terminator is not null;

    public int ExceptionRegionDepth => exceptionRegionStack.Count;

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
            blockExceptionOwners.Add(id, context);
        }

        return id;
    }

    public int CreateBlockOutsideCurrentExceptionRegion()
    {
        return CreateBlockAtExceptionDepth(
            Math.Max(0, exceptionRegionStack.Count - 1)
        );
    }

    public int CreateBlockAtExceptionDepth(int depth)
    {
        if (depth < 0 || depth > exceptionRegionStack.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }

        Stack<ExceptionRegionContext> suspendedContexts = [ ];

        while (exceptionRegionStack.Count > depth)
        {
            suspendedContexts.Push(exceptionRegionStack.Pop());
        }

        int blockId = CreateBlock();

        while (suspendedContexts.TryPop(out ExceptionRegionContext? context))
        {
            exceptionRegionStack.Push(context);
        }

        return blockId;
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
        AssignExceptionOwner(
            protectedEntry,
            new ExceptionRegionContext(id, IrExceptionRegionPart.Protected)
        );
        return id;
    }

    public (int EntryBlock, int ErrorSlot) BeginExceptionHandler(string? errorName)
    {
        ExceptionRegionContext context = GetExceptionRegionContext(
            IrExceptionRegionPart.Protected
        );
        exceptionRegionStack.Pop();
        exceptionRegionStack.Push(
            context with { Part = IrExceptionRegionPart.Handler }
        );
        int parentLifetimeRegion = lifetimeRegionStack.Peek();
        int handlerLifetimeRegion = lifetimeRegions.Count;
        lifetimeRegionStack.Push(handlerLifetimeRegion);
        int entryBlock = CreateBlock();
        lifetimeRegions.Add(
            new IrLifetimeRegion(
                handlerLifetimeRegion,
                parentLifetimeRegion,
                entryBlock
            )
        );
        int errorSlot = CreateSlot(
            IrSlotKind.Local,
            TypeSymbols.ErrorValue,
            errorName,
            IrSlotMutability.ReadOnly
        );
        MutableIrExceptionRegion region = exceptionRegions[context.RegionId];
        region.HandlerEntry = entryBlock;
        region.HandlerErrorSlot = errorSlot;
        return (entryBlock, errorSlot);
    }

    public void EndExceptionHandler()
    {
        _ = GetExceptionRegionContext(IrExceptionRegionPart.Handler);

        if (lifetimeRegionStack.Count == 1)
        {
            throw new InvalidOperationException(
                "The active exception handler has no lifetime region."
            );
        }

        lifetimeRegionStack.Pop();
    }

    public int BeginExceptionCleanup()
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
        int entryBlock = CreateBlock();
        MutableIrExceptionRegion region = exceptionRegions[context.RegionId];
        region.CleanupEntry = entryBlock;
        return entryBlock;
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

    private void AssignExceptionOwner(
        int blockId,
        ExceptionRegionContext owner
    )
    {
        if (
            blockExceptionOwners.TryGetValue(
                blockId,
                out ExceptionRegionContext? previousOwner
            )
        )
        {
            exceptionRegions[previousOwner.RegionId].RemoveBlock(
                previousOwner.Part,
                blockId
            );
        }

        exceptionRegions[owner.RegionId].AddBlock(owner.Part, blockId);
        blockExceptionOwners[blockId] = owner;
    }

    private sealed record ExceptionRegionContext(
        int RegionId,
        IrExceptionRegionPart Part
    );
}
