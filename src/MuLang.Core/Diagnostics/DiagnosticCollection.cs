using System.Collections;

namespace MuLang.Core.Diagnostics;

public sealed class DiagnosticCollection : IReadOnlyCollection<Diagnostic>
{
    private readonly IReadOnlyList<Diagnostic> diagnostics;

    private DiagnosticCollection(IReadOnlyList<Diagnostic> diagnostics)
    {
        this.diagnostics = diagnostics;
    }

    public static DiagnosticCollection Empty { get; } = new ([ ]);

    public int Count => diagnostics.Count;

    public Diagnostic this[int index] => diagnostics[index];

    public bool HasErrors => diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

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

    public IEnumerator<Diagnostic> GetEnumerator() => diagnostics.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
