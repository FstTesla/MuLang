using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.IR;

internal sealed record IrProgram(
    EnvironmentFingerprint EnvironmentFingerprint,
    IrFunction EntryFunction,
    IReadOnlyList<IrFunction> UserFunctions
)
{
    public TypeSymbol ResultType => EntryFunction.ReturnType;

    public int EntryBlock => EntryFunction.EntryBlock;

    public IReadOnlyList<IrSlot> Slots => EntryFunction.Slots;

    public IReadOnlyList<IrBasicBlock> Blocks => EntryFunction.Blocks;
}
