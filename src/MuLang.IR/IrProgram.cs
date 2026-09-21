using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Represents a complete portable MuLang IR program.</summary>
/// <param name="EnvironmentFingerprint">The fingerprint of the environment used during compilation.</param>
/// <param name="CompilationMode">The source compilation mode.</param>
/// <param name="LanguageProfileFingerprint">The fingerprint of the language profile used during compilation.</param>
/// <param name="EntryFunction">The program entry function.</param>
/// <param name="UserFunctions">The user-defined functions.</param>
public sealed record IrProgram(
    EnvironmentFingerprint EnvironmentFingerprint,
    CompilationMode CompilationMode,
    LanguageProfileFingerprint LanguageProfileFingerprint,
    IrFunction EntryFunction,
    IReadOnlyList<IrFunction> UserFunctions
)
{
    /// <summary>Gets the result type of the entry function.</summary>
    public TypeSymbol ResultType => EntryFunction.ReturnType;

    /// <summary>Gets the entry block identifier of the entry function.</summary>
    public int EntryBlock => EntryFunction.EntryBlock;

    /// <summary>Gets the slots of the entry function.</summary>
    public IReadOnlyList<IrSlot> Slots => EntryFunction.Slots;

    /// <summary>Gets the basic blocks of the entry function.</summary>
    public IReadOnlyList<IrBasicBlock> Blocks => EntryFunction.Blocks;
}
