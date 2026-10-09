using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record NamedTypeSyntax(SyntaxToken NameToken) : TypePrimarySyntax
{
    public override TextSpan Span => NameToken.Span;
}
