using MuLang.Core.Text;

namespace MuLang.Compiler.Binding;

internal sealed class FlowState
{
    public ISet<BoundVariableSymbol> Assigned { get; }

    public ISet<BoundVariableSymbol> PossiblyAssigned { get; }

    public ISet<(LocalSymbol Symbol, TextSpan Span)> ReadOnlyAssignments { get; }

    public bool CanCompleteNormally { get; set; }

    public FlowState(IEnumerable<BoundVariableSymbol> assigned, bool canCompleteNormally = true)
    {
        Assigned = new HashSet<BoundVariableSymbol>(assigned, ReferenceEqualityComparer.Instance);
        PossiblyAssigned = new HashSet<BoundVariableSymbol>(
            Assigned,
            ReferenceEqualityComparer.Instance
        );
        ReadOnlyAssignments = new HashSet<(LocalSymbol Symbol, TextSpan Span)>();
        CanCompleteNormally = canCompleteNormally;
    }

    public FlowState Clone()
    {
        FlowState clone = new (Assigned, CanCompleteNormally);
        clone.PossiblyAssigned.Clear();
        clone.PossiblyAssigned.UnionWith(PossiblyAssigned);
        clone.ReadOnlyAssignments.UnionWith(ReadOnlyAssignments);

        return clone;
    }
}
