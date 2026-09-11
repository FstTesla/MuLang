namespace MuLang.Compiler.Binding;

internal sealed class LoopFlowContext
{
    private readonly List<FlowState> breakStates = [ ];
    private readonly List<FlowState> continueStates = [ ];

    public IReadOnlyCollection<FlowState> BreakStates => breakStates;

    public IReadOnlyCollection<FlowState> ContinueStates => continueStates;

    public void AddBreakState(FlowState state)
    {
        breakStates.Add(state.Clone());
    }

    public void AddContinueState(FlowState state)
    {
        continueStates.Add(state.Clone());
    }
}
