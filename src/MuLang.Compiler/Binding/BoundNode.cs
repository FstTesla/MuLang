using MuLang.Compiler.Syntax;
using MuLang.Core.Text;

namespace MuLang.Compiler.Binding;

internal abstract record BoundNode(SyntaxNode Syntax)
{
    public TextSpan Span => Syntax.Span;
}
