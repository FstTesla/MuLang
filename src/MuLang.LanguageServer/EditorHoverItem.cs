namespace MuLang.LanguageServer;

internal sealed record EditorHoverItem(string Category, IReadOnlyList<EditorHoverPart> Parts)
{
    public string Content => string.Concat(Parts.Select(static part => part.Text));
}
