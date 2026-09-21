using MuLang.Core.Types;

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

        if (expression is not BoundExpression.Literal literal)
        {
            value = false;
            return false;
        }

        switch (literal.Type.Kind)
        {
            case TypeKind.Null:
            {
                value = false;
                return true;
            }

            case TypeKind.Bool when literal.Value is bool boolean:
            {
                value = boolean;
                return true;
            }

            case TypeKind.Int when literal.Value is long integer:
            {
                value = integer != 0;
                return true;
            }

            case TypeKind.Float when literal.Value is double number:
            {
                value = number != 0 && !double.IsNaN(number);
                return true;
            }

            case TypeKind.String when literal.Value is string text:
            {
                value = text.Length != 0;
                return true;
            }

            default:
            {
                value = false;
                return false;
            }
        }
    }
}
