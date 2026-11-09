using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal sealed record EditorHover(IReadOnlyList<EditorHoverItem> Items, TextSpan Span)
{
    public string Content => string.Join("\n\n", Items.Select(static item => $"[{item.Category}] {item.Content}"));
}
