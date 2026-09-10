using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal abstract record SyntaxNode
{
    public abstract TextSpan Span { get; }
}
