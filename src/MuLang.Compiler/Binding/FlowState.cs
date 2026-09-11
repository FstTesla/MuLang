namespace MuLang.Compiler.Binding;

internal sealed class FlowState
{
    public ISet<LocalSymbol> Assigned { get; }

    public bool CanCompleteNormally { get; set; }

    public FlowState(IEnumerable<LocalSymbol> assigned, bool canCompleteNormally = true)
    {
        Assigned = new HashSet<LocalSymbol>(assigned, ReferenceEqualityComparer.Instance);
        CanCompleteNormally = canCompleteNormally;
    }

    public FlowState Clone()
    {
        return new FlowState(Assigned, CanCompleteNormally);
    }
}
