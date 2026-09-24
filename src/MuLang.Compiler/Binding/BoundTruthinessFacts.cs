using MuLang.Core.Evaluation;

namespace MuLang.Compiler.Binding;

internal static class BoundTruthinessFacts
{
    public static bool TryEvaluate(
        BoundExpression expression,
        out bool value
    )
    {
        if (expression is BoundExpression.Truthiness truthiness)
        {
            return TryEvaluate(truthiness.Expression, out value);
        }

        if (expression is BoundExpression.Array or BoundExpression.Object)
        {
            value = true;
            return true;
        }

        if (
            expression is not BoundExpression.Literal literal ||
            !PrimitiveValueOperations.TryGetTruthiness(literal.Value, out value)
        )
        {
            value = false;
            return false;
        }

        return true;
    }
}
