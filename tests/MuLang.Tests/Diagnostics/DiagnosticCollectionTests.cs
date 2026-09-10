using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.Tests.Diagnostics;

public sealed class DiagnosticCollectionTests
{
    [Test]
    public void OrdersDiagnosticsBySpanAndPreservesReportOrderForEqualSpans()
    {
        var later = CreateDiagnostic("MUL003", DiagnosticSeverity.Warning, new TextSpan(8, 1));
        var firstAtSameSpan = CreateDiagnostic("MUL002", DiagnosticSeverity.Information, new TextSpan(2, 3));
        var earlier = CreateDiagnostic("MUL001", DiagnosticSeverity.Error, new TextSpan(2, 1));
        var secondAtSameSpan = CreateDiagnostic("MUL004", DiagnosticSeverity.Warning, new TextSpan(2, 3));

        var diagnostics = DiagnosticCollection.Create(
            [ later, firstAtSameSpan, earlier, secondAtSameSpan ]
        );

        Assert.That(
            diagnostics.Select(static diagnostic => diagnostic.Code),
            Is.EqualTo([ "MUL001", "MUL002", "MUL004", "MUL003" ])
        );
    }

    [Test]
    public void ReportsWhetherErrorsArePresent()
    {
        var diagnostics = DiagnosticCollection.Create(
            [ CreateDiagnostic("MUL001", DiagnosticSeverity.Error, new TextSpan(0, 1)) ]
        );

        Assert.That(diagnostics.HasErrors, Is.True);
    }

    private static Diagnostic CreateDiagnostic(
        string code,
        DiagnosticSeverity severity,
        TextSpan span
    )
    {
        return new Diagnostic(code, severity, DiagnosticCategory.Syntax, span, code);
    }
}
