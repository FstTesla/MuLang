using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed record SyntaxToken(TokenKind Kind, TextSpan Span, string? Value = null);
