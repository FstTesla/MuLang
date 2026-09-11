using MuLang.Compiler.Binding;
using MuLang.Compiler.Lowering;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;

namespace MuLang;

public static class MuLangCompiler
{
    public static CompilationResult CompileExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType = null
    )
    {
        if (expectedType?.Kind == TypeKind.Void)
        {
            throw new ArgumentException(
                "Expression mode cannot have void as its expected result type.",
                nameof(expectedType)
            );
        }

        return Compile(
            source,
            environment,
            CompilationMode.Expression,
            expectedType
        );
    }

    public static CompilationResult CompileProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType
    )
    {
        if (resultType is null)
        {
            throw new ArgumentNullException(nameof(resultType));
        }

        return Compile(
            source,
            environment,
            CompilationMode.Program,
            resultType
        );
    }

    private static CompilationResult Compile(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedType
    )
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        SyntaxTree syntaxTree = Parser.Parse(SourceText.From(source), compilationMode);
        BindingResult binding = Binder.Bind(syntaxTree, environment, expectedType);

        if (binding.Diagnostics.HasErrors)
        {
            return new CompilationResult(null, binding.Diagnostics);
        }

        LoweringResult lowering = Lowerer.Lower(binding, environment);

        if (lowering.Program is null)
        {
            return new CompilationResult(null, lowering.Diagnostics);
        }

        DotNetExportResult export = DotNetExporter.Export(
            lowering.Program,
            environment
        );
        DiagnosticCollection diagnostics =
            DiagnosticCollection.Create(
                binding.Diagnostics
                    .Concat(lowering.Diagnostics)
                    .Concat(export.Diagnostics)
            );

        return new CompilationResult(export.Delegate, diagnostics);
    }
}
