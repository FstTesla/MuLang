namespace MuLang.Compiler;

/// <summary>Represents one categorized item in semantic hover information.</summary>
public sealed class SemanticHoverItem
{
    internal SemanticHoverItem(
        SemanticHoverCategory category,
        IReadOnlyList<SemanticHoverPart> parts
    )
    {
        Category = category;
        Parts = parts;
        Content = string.Concat(parts.Select(static part => part.Text));
    }

    /// <summary>Gets the semantic category.</summary>
    public SemanticHoverCategory Category { get; }

    /// <summary>Gets the MuLang signature or expression description.</summary>
    public string Content { get; }

    /// <summary>Gets the classified signature parts.</summary>
    public IReadOnlyList<SemanticHoverPart> Parts { get; }
}
