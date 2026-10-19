namespace MuLang.Compiler;

internal static class SemanticHoverFormatting
{
    public static string Category(SemanticHoverCategory category)
    {
        return category switch
        {
            SemanticHoverCategory.Variable => "variable",
            SemanticHoverCategory.Parameter => "parameter",
            SemanticHoverCategory.Function => "function",
            SemanticHoverCategory.Type => "type",
            SemanticHoverCategory.Property => "property",
            SemanticHoverCategory.Literal => "literal",
            SemanticHoverCategory.Expression => "expression",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }

    public static string PlainText(
        IReadOnlyList<SemanticHoverItem> items
    )
    {
        return string.Join(
            "\n",
            items.Select(
                static item =>
                    $"[{Category(item.Category)}] {item.Content}"
            )
        );
    }
}
