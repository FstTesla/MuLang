using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal sealed record EditorDocumentSymbol(
    string Name,
    int Kind,
    TextSpan Range,
    TextSpan SelectionRange,
    IReadOnlyList<EditorDocumentSymbol> Children
);
