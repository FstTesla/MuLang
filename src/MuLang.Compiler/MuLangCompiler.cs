using MuLang.Compiler.Binding;
using MuLang.Compiler.Lowering;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler;

/// <summary>Compiles MuLang source code into runtime-independent portable IR.</summary>
public static class MuLangCompiler
{
    /// <summary>Compiles MuLang source code using the specified compilation settings.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="expectedResultType">The expected result type, or <c>null</c> to infer an expression result or use <c>void</c> for a program.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Version2" />.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compilationMode" /> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when the result type or language version is incompatible with the compilation settings.</exception>
    public static CompilationResult Compile(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedResultType = null,
        LanguageProfile? profile = null
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

        if (!Enum.IsDefined(compilationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(compilationMode));
        }

        profile ??= LanguageProfiles.Version2;

        if (environment.LanguageVersion != profile.LanguageVersion)
        {
            throw new ArgumentException(
                "The language profile version must match the environment language version.",
                nameof(profile)
            );
        }

        if (
            compilationMode == CompilationMode.Expression &&
            expectedResultType?.Kind == TypeKind.Void
        )
        {
            throw new ArgumentException(
                "Expression mode cannot have void as its expected result type.",
                nameof(expectedResultType)
            );
        }

        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            compilationMode,
            profile
        );
        BindingResult binding = Binder.Bind(
            syntaxTree,
            environment,
            expectedResultType
        );

        if (binding.Diagnostics.HasErrors)
        {
            return new CompilationResult(null, binding.Diagnostics);
        }

        LoweringResult lowering = Lowerer.Lower(binding, environment);
        DiagnosticCollection diagnostics = DiagnosticCollection.Create(
            binding.Diagnostics.Concat(lowering.Diagnostics)
        );

        return new CompilationResult(lowering.Program, diagnostics);
    }
}
