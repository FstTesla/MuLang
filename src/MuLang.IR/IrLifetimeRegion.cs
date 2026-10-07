namespace MuLang.IR;

/// <summary>Represents a scoped slot lifetime region in a MuLang IR function.</summary>
/// <param name="Id">The region identifier within the function.</param>
/// <param name="ParentRegion">The parent region identifier, or <c>null</c> for the root region.</param>
/// <param name="EntryBlock">The region entry block identifier.</param>
public sealed record IrLifetimeRegion(
    int Id,
    int? ParentRegion,
    int EntryBlock
);
