using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record FunctionDeclarationSyntax(
    SyntaxToken FuncKeyword,
    SyntaxToken IdentifierToken,
    SyntaxToken OpenParenthesisToken,
    IReadOnlyList<ParameterSyntax> Parameters,
    IReadOnlyList<SyntaxToken> CommaTokens,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken ColonToken,
    TypeSyntax ReturnType,
    BlockStatementSyntax Body
) : SyntaxNode
{
    public override TextSpan Span => TextSpan.FromBounds(
        FuncKeyword.Span.Start,
        Body.Span.End
    );
}
