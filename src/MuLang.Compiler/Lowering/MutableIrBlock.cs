using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal sealed class MutableIrBlock
{
    private readonly List<IrInstruction> instructions = [];

    public MutableIrBlock(int id)
    {
        Id = id;
    }

    public int Id { get; }

    public IReadOnlyList<IrInstruction> Instructions => instructions;

    public IrTerminator? Terminator { get; set; }

    public void Add(IrInstruction instruction)
    {
        instructions.Add(instruction);
    }

    public IrBasicBlock Build()
    {
        if (Terminator is null)
        {
            throw new InvalidOperationException($"IR block {Id} has no terminator.");
        }

        return new IrBasicBlock(
            Id,
            instructions.AsReadOnly(),
            Terminator
        );
    }
}
