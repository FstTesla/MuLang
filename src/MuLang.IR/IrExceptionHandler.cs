namespace MuLang.IR;

/// <summary>Represents the handler component of an exception region.</summary>
/// <param name="EntryBlock">The component entry block identifier.</param>
/// <param name="Blocks">The directly owned block identifiers.</param>
/// <param name="ErrorSlot">The read-only slot that receives the current error.</param>
public sealed record IrExceptionHandler(
    int EntryBlock,
    IReadOnlyCollection<int> Blocks,
    int ErrorSlot
);
