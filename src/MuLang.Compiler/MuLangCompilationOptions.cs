using MuLang.Core;
using MuLang.Core.Types;

namespace MuLang.Compiler;

/// <summary>Represents options shared by MuLang analysis and compilation.</summary>
public sealed class MuLangCompilationOptions
{
    /// <summary>Gets the default compilation options.</summary>
    public static MuLangCompilationOptions Default { get; } = new ();

    /// <summary>Gets or sets the expected result type, or <c>null</c> to infer an expression result or use <c>void</c> for a program.</summary>
    public TypeSymbol? ExpectedResultType { get; init; }

    /// <summary>Gets or sets the language profile, or <c>null</c> to use <see cref="LanguageProfiles.Latest" />.</summary>
    public LanguageProfile? Profile { get; init; }

    /// <summary>Gets or sets the source document name or URI, or <c>null</c> when unavailable.</summary>
    public string? SourceDocument { get; init; }
}
