namespace MuLang.Compiler.Syntax;

internal static class SyntaxFacts
{
    public static int GetUnaryPrecedence(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Plus => 13,
            TokenKind.Minus => 13,
            TokenKind.Bang => 13,
            TokenKind.Tilde => 13,
            _ => 0,
        };
    }

    public static int GetBinaryPrecedence(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Asterisk => 12,
            TokenKind.Slash => 12,
            TokenKind.Percent => 12,
            TokenKind.Plus => 11,
            TokenKind.Minus => 11,
            TokenKind.LeftShift => 10,
            TokenKind.RightShift => 10,
            TokenKind.AsKeyword => 9,
            TokenKind.LessThan => 8,
            TokenKind.LessThanOrEqual => 8,
            TokenKind.GreaterThan => 8,
            TokenKind.GreaterThanOrEqual => 8,
            TokenKind.IsKeyword => 8,
            TokenKind.HasKeyword => 8,
            TokenKind.EqualEqual => 7,
            TokenKind.BangEqual => 7,
            TokenKind.EqualEqualEqual => 7,
            TokenKind.BangEqualEqual => 7,
            TokenKind.Ampersand => 6,
            TokenKind.Caret => 5,
            TokenKind.Pipe => 4,
            TokenKind.AmpersandAmpersand => 3,
            TokenKind.PipePipe => 2,
            _ => 0,
        };
    }

    public static bool IsNonAssociativeBinaryOperator(TokenKind kind)
    {
        int precedence = GetBinaryPrecedence(kind);

        return precedence is 7 or 8;
    }

    public static bool IsTypeName(TokenKind kind)
    {
        return kind is
            TokenKind.BoolKeyword or
            TokenKind.IntKeyword or
            TokenKind.FloatKeyword or
            TokenKind.NumberKeyword or
            TokenKind.StringKeyword or
            TokenKind.UnknownKeyword or
            TokenKind.ObjectKeyword or
            TokenKind.Identifier;
    }

    public static bool IsNumericLiteral(TokenKind kind)
    {
        return kind is TokenKind.IntegerLiteral or TokenKind.NumberLiteral;
    }

    public static bool IsAssignmentTarget(ExpressionSyntax expression)
    {
        return expression is
                NameExpressionSyntax or
                MemberAccessExpressionSyntax or
                ElementAccessExpressionSyntax &&
            !ContainsOptionalAccess(expression);
    }

    public static bool IsRemovalTarget(ExpressionSyntax expression)
    {
        return expression is MemberAccessExpressionSyntax or ElementAccessExpressionSyntax &&
            !ContainsOptionalAccess(expression);
    }

    public static bool CanStartExpression(TokenKind kind)
    {
        return kind is
            TokenKind.Identifier or
            TokenKind.IntegerLiteral or
            TokenKind.NumberLiteral or
            TokenKind.StringLiteral or
            TokenKind.TrueKeyword or
            TokenKind.FalseKeyword or
            TokenKind.NullKeyword or
            TokenKind.OpenParenthesis or
            TokenKind.OpenBracket or
            TokenKind.OpenBrace or
            TokenKind.OpenObjectBrace or
            TokenKind.Plus or
            TokenKind.Minus or
            TokenKind.Bang or
            TokenKind.Tilde;
    }

    private static bool ContainsOptionalAccess(ExpressionSyntax expression)
    {
        return expression switch
        {
            MemberAccessExpressionSyntax member =>
                member.IsOptional || ContainsOptionalAccess(member.Target),
            ElementAccessExpressionSyntax element =>
                element.IsOptional || ContainsOptionalAccess(element.Target),
            _ => false,
        };
    }
}
