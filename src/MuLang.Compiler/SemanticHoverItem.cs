namespace MuLang.Compiler;

/// <summary>Represents one categorized item in semantic hover information.</summary>
public sealed class SemanticHoverItem
{
    internal SemanticHoverItem(
        SemanticHoverCategory category,
        string content
    )
    {
        Category = category;
        Content = content;
    }

    /// <summary>Gets the semantic category.</summary>
    public SemanticHoverCategory Category { get; }

    /// <summary>Gets the MuLang signature or expression description.</summary>
    public string Content { get; }
}
