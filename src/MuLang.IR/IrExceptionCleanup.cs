namespace MuLang.IR;

/// <summary>Represents the cleanup component of an exception region.</summary>
/// <param name="EntryBlock">The component entry block identifier.</param>
/// <param name="Blocks">The directly owned block identifiers.</param>
public sealed record IrExceptionCleanup(
    int EntryBlock,
    IReadOnlyCollection<int> Blocks
);
