namespace MuLang.IR;

/// <summary>Represents a protected exception-handling region in a MuLang IR function.</summary>
/// <param name="Id">The region identifier within the function.</param>
/// <param name="ParentRegion">The parent exception-region identifier, or <c>null</c>.</param>
/// <param name="ParentPart">The parent component that contains this region, or <c>null</c>.</param>
/// <param name="Protected">The protected component.</param>
/// <param name="Handler">The optional handler component.</param>
/// <param name="Cleanup">The optional cleanup component.</param>
public sealed record IrExceptionRegion(
    int Id,
    int? ParentRegion,
    IrExceptionRegionPart? ParentPart,
    IrExceptionProtectedRegion Protected,
    IrExceptionHandler? Handler,
    IrExceptionCleanup? Cleanup
);
