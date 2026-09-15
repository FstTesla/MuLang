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

/// <summary>Provides methods for compiling MuLang source code.</summary>
public static class MuLangCompiler
{
    /// <summary>Compiles a MuLang expression using the default language profile.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="expectedType">The expected expression result type, or <c>null</c> to infer it.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expectedType" /> is void or the environment language version is incompatible.</exception>
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
            LanguageProfiles.Version1,
            CompilationMode.Expression,
            expectedType
        );
    }

    /// <summary>Compiles a MuLang expression using the specified language profile.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="expectedType">The expected expression result type, or <c>null</c> to infer it.</param>
    /// <param name="profile">The language profile used for compilation.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" />, <paramref name="environment" />, or <paramref name="profile" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="expectedType" /> is void or the profile language version does not match the environment.</exception>
    public static CompilationResult CompileExpression(
        string source,
        EnvironmentSchema environment,
        TypeSymbol? expectedType,
        LanguageProfile profile
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
            profile,
            CompilationMode.Expression,
            expectedType
        );
    }

    /// <summary>Compiles a MuLang program using the default language profile.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="resultType">The required program result type.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the environment language version is incompatible.</exception>
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
            LanguageProfiles.Version1,
            CompilationMode.Program,
            resultType
        );
    }

    /// <summary>Compiles a MuLang program using the specified language profile.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="resultType">The required program result type.</param>
    /// <param name="profile">The language profile used for compilation.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the profile language version does not match the environment.</exception>
    public static CompilationResult CompileProgram(
        string source,
        EnvironmentSchema environment,
        TypeSymbol resultType,
        LanguageProfile profile
    )
    {
        if (resultType is null)
        {
            throw new ArgumentNullException(nameof(resultType));
        }

        return Compile(
            source,
            environment,
            profile,
            CompilationMode.Program,
            resultType
        );
    }

    private static CompilationResult Compile(
        string source,
        EnvironmentSchema environment,
        LanguageProfile profile,
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

        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (environment.LanguageVersion != profile.LanguageVersion)
        {
            throw new ArgumentException(
                "The language profile version must match the environment language version.",
                nameof(profile)
            );
        }

        SyntaxTree syntaxTree = Parser.Parse(
            SourceText.From(source),
            compilationMode,
            profile
        );
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
            environment,
            compilationMode,
            profile.Fingerprint
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
