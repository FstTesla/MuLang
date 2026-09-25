namespace MuLang.Compiler.Syntax;

internal static class SyntaxFacts
{
    public static int GetUnaryPrecedence(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Plus => 14,
            TokenKind.Minus => 14,
            TokenKind.Bang => 14,
            TokenKind.Tilde => 14,
            _ => 0,
        };
    }

    public static int GetBinaryPrecedence(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Asterisk => 13,
            TokenKind.Slash => 13,
            TokenKind.Percent => 13,
            TokenKind.Plus => 12,
            TokenKind.Minus => 12,
            TokenKind.LeftShift => 11,
            TokenKind.RightShift => 11,
            TokenKind.AsKeyword => 10,
            TokenKind.LessThan => 9,
            TokenKind.LessThanOrEqual => 9,
            TokenKind.GreaterThan => 9,
            TokenKind.GreaterThanOrEqual => 9,
            TokenKind.IsKeyword => 9,
            TokenKind.HasKeyword => 9,
            TokenKind.EqualEqual => 8,
            TokenKind.BangEqual => 8,
            TokenKind.EqualEqualEqual => 8,
            TokenKind.BangEqualEqual => 8,
            TokenKind.Ampersand => 7,
            TokenKind.Caret => 6,
            TokenKind.Pipe => 5,
            TokenKind.AmpersandAmpersand => 4,
            TokenKind.PipePipe => 3,
            TokenKind.QuestionQuestion => 2,
            _ => 0,
        };
    }

    public static bool IsNonAssociativeBinaryOperator(TokenKind kind)
    {
        int precedence = GetBinaryPrecedence(kind);

        return precedence is 8 or 9;
    }

    public static bool IsRightAssociativeBinaryOperator(TokenKind kind)
    {
        return kind == TokenKind.QuestionQuestion;
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
        return kind is
            TokenKind.IntegerLiteral or
            TokenKind.NumberLiteral or
            TokenKind.InftyKeyword or
            TokenKind.NanKeyword;
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
            TokenKind.InftyKeyword or
            TokenKind.NanKeyword or
            TokenKind.StringLiteral or
            TokenKind.TrueKeyword or
            TokenKind.FalseKeyword or
            TokenKind.NullKeyword or
            TokenKind.OpenParenthesis or
            TokenKind.OpenBracket or
            TokenKind.ReadOnlyOpenBracket or
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
