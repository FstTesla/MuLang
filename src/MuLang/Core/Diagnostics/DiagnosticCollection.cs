using System.Collections;

namespace MuLang.Core.Diagnostics;

/// <summary>Represents an ordered, immutable collection of diagnostics.</summary>
public sealed class DiagnosticCollection : IReadOnlyList<Diagnostic>
{
    private readonly IReadOnlyList<Diagnostic> diagnostics;

    private DiagnosticCollection(IReadOnlyList<Diagnostic> diagnostics)
    {
        this.diagnostics = diagnostics;
    }

    /// <summary>Gets the empty diagnostic collection.</summary>
    public static DiagnosticCollection Empty { get; } = new ([ ]);

    /// <summary>Gets the number of diagnostics.</summary>
    public int Count => diagnostics.Count;

    /// <summary>Gets the diagnostic at the specified index.</summary>
    /// <param name="index">The zero-based diagnostic index.</param>
    public Diagnostic this[int index] => diagnostics[index];

    /// <summary>Gets a value indicating whether the collection contains any error diagnostics.</summary>
    public bool HasErrors => diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    /// <summary>Creates an ordered diagnostic collection.</summary>
    /// <param name="diagnostics">The diagnostics to include.</param>
    /// <returns>The ordered diagnostic collection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostics" /> is <c>null</c>.</exception>
    public static DiagnosticCollection Create(IEnumerable<Diagnostic> diagnostics)
    {
        if (diagnostics is null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        IReadOnlyList<Diagnostic> orderedDiagnostics =
        [
            .. diagnostics
                .Select(static (diagnostic, index) => (Diagnostic: diagnostic, Index: index))
                .OrderBy(static item => item.Diagnostic.Span.Start)
                .ThenBy(static item => item.Diagnostic.Span.Length)
                .ThenBy(static item => item.Index)
                .Select(static item => item.Diagnostic),
        ];

        return orderedDiagnostics.Count > 0
            ? new DiagnosticCollection(orderedDiagnostics)
            : Empty;
    }

    /// <inheritdoc />
    public IEnumerator<Diagnostic> GetEnumerator() => diagnostics.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
