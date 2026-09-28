using MuLang.Compiler.Binding;
using MuLang.Compiler.Lowering;
using MuLang.Compiler.Optimization;
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
    /// <summary>Analyzes MuLang source code using the specified compilation settings without producing portable IR.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the analyzed code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="expectedResultType">The expected result type, or <c>null</c> to infer an expression result or use <c>void</c> for a program.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Version1_1" />.</param>
    /// <returns>The analysis result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compilationMode" /> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when the result type or language version is incompatible with the analysis settings.</exception>
    public static AnalysisResult Analyze(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedResultType = null,
        LanguageProfile? profile = null
    )
    {
        (BindingResult _, DiagnosticCollection diagnostics) = BindAndOptimize(
            source,
            environment,
            compilationMode,
            expectedResultType,
            profile
        );

        return new AnalysisResult(diagnostics);
    }

    /// <summary>Classifies MuLang source text using the specified language profile.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Version1_1" />.</param>
    /// <returns>The lexical classification result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> is <c>null</c>.</exception>
    public static ClassificationResult Classify(
        string source,
        LanguageProfile? profile = null
    )
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        profile ??= LanguageProfiles.Version1_1;
        LexResult lexing = Lexer.Lex(SourceText.From(source), profile);
        IReadOnlyList<SourceClassification> classifications =
        [
            .. lexing.Tokens
                .Where(static token => token.Kind != TokenKind.EndOfFile)
                .Select(
                    static token => new SourceClassification(
                        ClassifyToken(token.Kind),
                        token.Span
                    )
                ),
        ];

        return new ClassificationResult(classifications, lexing.Diagnostics);
    }

    /// <summary>Classifies MuLang identifiers using binding information from the specified compilation settings.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the classified code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="expectedResultType">The expected result type, or <c>null</c> to infer an expression result or use <c>void</c> for a program.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Version1_1" />.</param>
    /// <returns>The semantic classification result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compilationMode" /> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when the result type or language version is incompatible with the classification settings.</exception>
    public static SemanticClassificationResult ClassifySemantically(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedResultType = null,
        LanguageProfile? profile = null
    )
    {
        BindingResult binding = Bind(
            source,
            environment,
            compilationMode,
            expectedResultType,
            profile
        );

        return new SemanticClassificationResult(
            SemanticClassifier.Classify(binding),
            binding.Diagnostics
        );
    }

    /// <summary>Compiles MuLang source code using the specified compilation settings.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="expectedResultType">The expected result type, or <c>null</c> to infer an expression result or use <c>void</c> for a program.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Version1_1" />.</param>
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
        (
            BindingResult optimizedBinding,
            DiagnosticCollection analysisDiagnostics
        ) = BindAndOptimize(
            source,
            environment,
            compilationMode,
            expectedResultType,
            profile
        );

        if (analysisDiagnostics.HasErrors)
        {
            return new CompilationResult(null, analysisDiagnostics);
        }

        LoweringResult lowering = Lowerer.Lower(optimizedBinding, environment);
        DiagnosticCollection diagnostics = DiagnosticCollection.Create(
            analysisDiagnostics.Concat(lowering.Diagnostics)
        );

        return new CompilationResult(lowering.Program, diagnostics);
    }

    private static (
        BindingResult Binding,
        DiagnosticCollection Diagnostics
        ) BindAndOptimize(
            string source,
            EnvironmentSchema environment,
            CompilationMode compilationMode,
            TypeSymbol? expectedResultType,
            LanguageProfile? profile
        )
    {
        BindingResult binding = Bind(
            source,
            environment,
            compilationMode,
            expectedResultType,
            profile
        );

        if (binding.Diagnostics.HasErrors)
        {
            return (binding, binding.Diagnostics);
        }

        LanguageProfile effectiveProfile = profile ?? LanguageProfiles.Version1_1;
        BindingResult optimizedBinding = binding;
        DiagnosticCollection optimizationDiagnostics = binding.Diagnostics;

        if (effectiveProfile.ConstantFolding == ConstantFoldingFeature.Enabled)
        {
            ConstantFoldingResult folding = ConstantFolder.Fold(binding);
            optimizationDiagnostics = DiagnosticCollection.Create(
                binding.Diagnostics.Concat(folding.Diagnostics)
            );

            if (optimizationDiagnostics.HasErrors)
            {
                return (folding.Binding, optimizationDiagnostics);
            }

            optimizedBinding = folding.Binding;
        }

        return (optimizedBinding, optimizationDiagnostics);
    }

    private static BindingResult Bind(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedResultType,
        LanguageProfile? profile
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

        profile ??= LanguageProfiles.Version1_1;

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
        return binding;
    }

    private static SourceClassificationKind ClassifyToken(TokenKind kind)
    {
        return kind switch
        {
            TokenKind.Bad => SourceClassificationKind.Invalid,
            TokenKind.Identifier => SourceClassificationKind.Identifier,
            TokenKind.IntegerLiteral or
                TokenKind.NumberLiteral or
                TokenKind.InftyKeyword or
                TokenKind.NanKeyword => SourceClassificationKind.Number,
            TokenKind.StringLiteral => SourceClassificationKind.String,
            TokenKind.AsKeyword or
                TokenKind.HasKeyword or
                TokenKind.IsKeyword or
                TokenKind.QuestionQuestion or
                TokenKind.OptionalDot or
                TokenKind.OptionalOpenBracket or
                TokenKind.OptionalPropertyColon or
                TokenKind.Plus or
                TokenKind.Minus or
                TokenKind.Asterisk or
                TokenKind.Slash or
                TokenKind.Percent or
                TokenKind.LessThan or
                TokenKind.LessThanOrEqual or
                TokenKind.GreaterThan or
                TokenKind.GreaterThanOrEqual or
                TokenKind.LeftShift or
                TokenKind.RightShift or
                TokenKind.Equal or
                TokenKind.EqualEqual or
                TokenKind.EqualEqualEqual or
                TokenKind.Bang or
                TokenKind.BangEqual or
                TokenKind.BangEqualEqual or
                TokenKind.Ampersand or
                TokenKind.AmpersandAmpersand or
                TokenKind.Caret or
                TokenKind.Pipe or
                TokenKind.PipePipe or
                TokenKind.Tilde => SourceClassificationKind.Operator,
            TokenKind.BoolKeyword or
                TokenKind.BreakKeyword or
                TokenKind.ContinueKeyword or
                TokenKind.ElseKeyword or
                TokenKind.FalseKeyword or
                TokenKind.FloatKeyword or
                TokenKind.ForKeyword or
                TokenKind.FuncKeyword or
                TokenKind.IfKeyword or
                TokenKind.IntKeyword or
                TokenKind.NullKeyword or
                TokenKind.NumberKeyword or
                TokenKind.ObjectKeyword or
                TokenKind.ReturnKeyword or
                TokenKind.StringKeyword or
                TokenKind.TrueKeyword or
                TokenKind.UnknownKeyword or
                TokenKind.VarKeyword or
                TokenKind.VoidKeyword or
                TokenKind.WhileKeyword => SourceClassificationKind.Keyword,
            _ => SourceClassificationKind.Punctuation,
        };
    }
}
