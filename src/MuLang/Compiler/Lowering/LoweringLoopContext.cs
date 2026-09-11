namespace MuLang.Compiler.Lowering;

internal sealed class LoweringLoopContext
{
    private readonly Func<int> createBreakBlock;
    private int? breakBlock;

    public LoweringLoopContext(
        int continueBlock,
        int? breakBlock,
        Func<int> createBreakBlock
    )
    {
        ContinueBlock = continueBlock;
        this.breakBlock = breakBlock;
        this.createBreakBlock = createBreakBlock;
    }

    public int ContinueBlock { get; }

    public bool HasBreakBlock => breakBlock is not null;

    public int BreakBlock => breakBlock ??= createBreakBlock();
}
