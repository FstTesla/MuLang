using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record ProgramRootSyntax(
    IReadOnlyList<TypeDeclarationSyntax> Types,
    IReadOnlyList<FunctionDeclarationSyntax> Functions,
    IReadOnlyList<StatementSyntax> Statements,
    SyntaxToken EndOfFileToken
) : RootSyntax
{
    public override TextSpan Span
    {
        get
        {
            int start = Types is [ var firstType, .. ]
                ? firstType.Span.Start
                : Functions is [ var firstFunction, .. ]
                ? firstFunction.Span.Start
                : Statements is [ var firstStatement, .. ]
                    ? firstStatement.Span.Start
                    : EndOfFileToken.Span.Start;

            return TextSpan.FromBounds(start, EndOfFileToken.Span.End);
        }
    }
}
