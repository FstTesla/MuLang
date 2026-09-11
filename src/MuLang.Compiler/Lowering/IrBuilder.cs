using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal sealed class IrBuilder
{
    private readonly List<IrSlot> slots = [ ];
    private readonly List<MutableIrBlock> blocks = [ ];

    public IrBuilder()
    {
        EntryBlock = CreateBlock();
        CurrentBlock = GetBlock(EntryBlock);
    }

    public int EntryBlock { get; }

    public MutableIrBlock? CurrentBlock { get; private set; }

    public int CreateBlock()
    {
        int id = blocks.Count;
        blocks.Add(new MutableIrBlock(id));

        return id;
    }

    public void SwitchTo(int blockId)
    {
        CurrentBlock = GetBlock(blockId);
    }

    public void SetUnreachable()
    {
        CurrentBlock = null;
    }

    public int CreateSlot(IrSlotKind kind, TypeSymbol type, string? name = null)
    {
        int id = slots.Count;
        slots.Add(new IrSlot(id, kind, type, name));

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

    public bool IsCurrentTerminated => CurrentBlock?.Terminator is not null;

    public IrProgram Build(
        EnvironmentFingerprint environmentFingerprint,
        TypeSymbol resultType
    )
    {
        IrBasicBlock[] immutableBlocks = [ .. blocks.Select(static block => block.Build()) ];

        return new IrProgram(
            environmentFingerprint,
            resultType,
            EntryBlock,
            slots.AsReadOnly(),
            Array.AsReadOnly(immutableBlocks)
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
}
