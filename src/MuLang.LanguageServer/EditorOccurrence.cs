using MuLang.Compiler;
using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal sealed record EditorOccurrence(
    object? Symbol,
    int Kind,
    SemanticOccurrenceRole Role,
    SemanticClassificationModifiers Modifiers,
    TextSpan Span,
    IReadOnlyList<EditorHoverItem> HoverItems,
    TextSpan? DefinitionSpan,
    bool IsClassification
);
