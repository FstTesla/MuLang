namespace MuLang.Compiler.Binding;

internal sealed class FlowState
{
    public ISet<BoundVariableSymbol> Assigned { get; }

    public bool CanCompleteNormally { get; set; }

    public FlowState(IEnumerable<BoundVariableSymbol> assigned, bool canCompleteNormally = true)
    {
        Assigned = new HashSet<BoundVariableSymbol>(assigned, ReferenceEqualityComparer.Instance);
        CanCompleteNormally = canCompleteNormally;
    }

    public FlowState Clone()
    {
        return new FlowState(Assigned, CanCompleteNormally);
    }
}
