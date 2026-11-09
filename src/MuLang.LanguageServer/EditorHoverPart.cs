using MuLang.Compiler;

namespace MuLang.LanguageServer;

internal sealed record EditorHoverPart(SemanticHoverPartKind Kind, string Text);
