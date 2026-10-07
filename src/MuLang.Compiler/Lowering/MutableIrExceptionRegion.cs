using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal sealed class MutableIrExceptionRegion
{
    private readonly IList<int> protectedBlocks = [ ];
    private readonly IList<int> handlerBlocks = [ ];
    private readonly IList<int> cleanupBlocks = [ ];

    public MutableIrExceptionRegion(
        int id,
        int? parentRegion,
        IrExceptionRegionPart? parentPart,
        int protectedEntry
    )
    {
        Id = id;
        ParentRegion = parentRegion;
        ParentPart = parentPart;
        ProtectedEntry = protectedEntry;
    }

    public int Id { get; }

    public int? ParentRegion { get; }

    public IrExceptionRegionPart? ParentPart { get; }

    public int ProtectedEntry { get; }

    public int? HandlerEntry { get; set; }

    public int? HandlerErrorSlot { get; set; }

    public int? CleanupEntry { get; set; }

    public void AddBlock(IrExceptionRegionPart part, int blockId)
    {
        GetBlocks(part).Add(blockId);
    }

    public IrExceptionRegion Build()
    {
        return new IrExceptionRegion(
            Id,
            ParentRegion,
            ParentPart,
            new IrExceptionProtectedRegion(
                ProtectedEntry,
                protectedBlocks.AsReadOnly()
            ),
            HandlerEntry is int handlerEntry &&
            HandlerErrorSlot is int handlerErrorSlot
                ? new IrExceptionHandler(
                    handlerEntry,
                    handlerBlocks.AsReadOnly(),
                    handlerErrorSlot
                )
                : null,
            CleanupEntry is int cleanupEntry
                ? new IrExceptionCleanup(
                    cleanupEntry,
                    cleanupBlocks.AsReadOnly()
                )
                : null
        );
    }

    private IList<int> GetBlocks(IrExceptionRegionPart part)
    {
        return part switch
        {
            IrExceptionRegionPart.Protected => protectedBlocks,
            IrExceptionRegionPart.Handler => handlerBlocks,
            IrExceptionRegionPart.Cleanup => cleanupBlocks,
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };
    }
}
