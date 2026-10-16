using MuLang.Compiler.Binding;
using MuLang.Compiler.Lowering;
using MuLang.Compiler.Optimization;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Runtime.CompilerServices;

namespace MuLang.Compiler;

/// <summary>Compiles MuLang source code into runtime-independent portable IR.</summary>
public static class MuLangCompiler
{
    /// <summary>Analyzes MuLang source code using the specified compilation settings without producing portable IR.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the analyzed code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="expectedResultType">The expected result type, or <c>null</c> to infer an expression result or use <c>void</c> for a program.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Latest" />.</param>
    /// <returns>The analysis result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compilationMode" /> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when the result type or language version is incompatible with the analysis settings.</exception>
    [Obsolete("Use Analyze(string, EnvironmentSchema, CompilationMode, MuLangCompilationOptions) instead.")]
    public static AnalysisResult Analyze(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedResultType = null,
        LanguageProfile? profile = null
    )
    {
        return Analyze(
            source,
            environment,
            compilationMode,
            new MuLangCompilationOptions
            {
                ExpectedResultType = expectedResultType,
                Profile = profile,
            }
        );
    }

    /// <summary>Analyzes MuLang source code using the specified compilation options without producing portable IR.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the analyzed code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="options">The analysis options, or <c>null</c> to use <see cref="MuLangCompilationOptions.Default" />.</param>
    /// <returns>The analysis result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the source document URI is not absolute, or the result type or language version is incompatible with the analysis settings.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compilationMode" /> is not defined.</exception>
    [OverloadResolutionPriority(1)]
    public static AnalysisResult Analyze(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        MuLangCompilationOptions? options = null
    )
    {
        options ??= MuLangCompilationOptions.Default;

        SourceText sourceText = CreateSourceText(source, options.SourceDocument);
        (BindingResult _, DiagnosticCollection diagnostics) = BindAndOptimize(
            sourceText,
            environment,
            compilationMode,
            options.ExpectedResultType,
            options.Profile
        );

        return new AnalysisResult(
            diagnostics,
            options.SourceDocument,
            sourceText.LineMap
        );
    }

    /// <summary>Classifies MuLang source text using the specified language profile.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Latest" />.</param>
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

        profile ??= LanguageProfiles.Latest;
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
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Latest" />.</param>
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
            CreateSourceText(source, null),
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

    /// <summary>Gets semantic hover information at a Unicode scalar offset.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the source code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="offset">The zero-based Unicode scalar offset.</param>
    /// <param name="options">The analysis options, or <c>null</c> to use <see cref="MuLangCompilationOptions.Default" />.</param>
    /// <returns>The hover information, or <c>null</c> when no resolved symbol covers the offset.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the offset is outside the source or the compilation mode is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when the source document URI, result type, or language version is incompatible with the analysis settings.</exception>
    public static HoverInfo? GetHover(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        int offset,
        MuLangCompilationOptions? options = null
    )
    {
        options ??= MuLangCompilationOptions.Default;
        SourceText sourceText = CreateSourceText(source, options.SourceDocument);

        if (offset < 0 || offset > sourceText.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        BindingResult binding = Bind(
            sourceText,
            environment,
            compilationMode,
            options.ExpectedResultType,
            options.Profile
        );
        HoverInfo? hover = null;
        SemanticClassifier.Classify(
            binding,
            offset,
            (span, content) =>
            {
                hover = new HoverInfo(content, span);
            }
        );
        return hover;
    }

    /// <summary>Compiles MuLang source code using the specified compilation settings.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="expectedResultType">The expected result type, or <c>null</c> to infer an expression result or use <c>void</c> for a program.</param>
    /// <param name="profile">The language profile, or <c>null</c> to use <see cref="LanguageProfiles.Latest" />.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compilationMode" /> is not defined.</exception>
    /// <exception cref="ArgumentException">Thrown when the result type or language version is incompatible with the compilation settings.</exception>
    [Obsolete("Use Compile(string, EnvironmentSchema, CompilationMode, MuLangCompilationOptions) instead.")]
    public static CompilationResult Compile(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedResultType = null,
        LanguageProfile? profile = null
    )
    {
        return Compile(
            source,
            environment,
            compilationMode,
            new MuLangCompilationOptions
            {
                ExpectedResultType = expectedResultType,
                Profile = profile,
            }
        );
    }

    /// <summary>Compiles MuLang source code using the specified compilation options.</summary>
    /// <param name="source">The MuLang source code.</param>
    /// <param name="environment">The environment schema available to the compiled code.</param>
    /// <param name="compilationMode">The compilation mode.</param>
    /// <param name="options">The compilation options, or <c>null</c> to use <see cref="MuLangCompilationOptions.Default" />.</param>
    /// <returns>The compilation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source" /> or <paramref name="environment" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when the source document URI is not absolute, or the result type or language version is incompatible with the compilation settings.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compilationMode" /> is not defined.</exception>
    [OverloadResolutionPriority(1)]
    public static CompilationResult Compile(
        string source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        MuLangCompilationOptions? options = null
    )
    {
        options ??= MuLangCompilationOptions.Default;

        SourceText sourceText = CreateSourceText(source, options.SourceDocument);
        (
            BindingResult optimizedBinding,
            DiagnosticCollection analysisDiagnostics
        ) = BindAndOptimize(
            sourceText,
            environment,
            compilationMode,
            options.ExpectedResultType,
            options.Profile
        );

        if (analysisDiagnostics.HasErrors)
        {
            return new CompilationResult(
                null,
                analysisDiagnostics,
                options.SourceDocument,
                sourceText.LineMap
            );
        }

        LoweringResult lowering = Lowerer.Lower(
            optimizedBinding,
            environment,
            options.SourceDocument,
            sourceText.LineMap
        );
        DiagnosticCollection diagnostics = DiagnosticCollection.Create(
            analysisDiagnostics.Concat(lowering.Diagnostics)
        );

        return new CompilationResult(
            lowering.Program,
            diagnostics,
            options.SourceDocument,
            sourceText.LineMap
        );
    }

    private static (
        BindingResult Binding,
        DiagnosticCollection Diagnostics
        ) BindAndOptimize(
            SourceText source,
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

        LanguageProfile effectiveProfile = profile ?? LanguageProfiles.Latest;
        BindingResult optimizedBinding = binding;
        DiagnosticCollection optimizationDiagnostics;
        ConstantFoldingResult folding = ConstantFolder.Fold(binding);

        if (effectiveProfile.ConstantFolding == ConstantFoldingFeature.Enabled)
        {
            optimizationDiagnostics = DiagnosticCollection.Create(
                binding.Diagnostics.Concat(folding.Diagnostics)
            );

            if (optimizationDiagnostics.HasErrors)
            {
                return (folding.Binding, optimizationDiagnostics);
            }

            optimizedBinding = folding.Binding;
        }
        else
        {
            optimizationDiagnostics = DiagnosticCollection.Create(
                binding.Diagnostics.Concat(
                    folding.Diagnostics.Select(
                        static diagnostic => new Diagnostic(
                            diagnostic.Code,
                            DiagnosticSeverity.Warning,
                            diagnostic.Category,
                            diagnostic.Span,
                            diagnostic.Message
                        )
                    )
                )
            );
        }

        return (optimizedBinding, optimizationDiagnostics);
    }

    private static BindingResult Bind(
        SourceText source,
        EnvironmentSchema environment,
        CompilationMode compilationMode,
        TypeSymbol? expectedResultType,
        LanguageProfile? profile
    )
    {
        if (environment is null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (!Enum.IsDefined(compilationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(compilationMode));
        }

        profile ??= LanguageProfiles.Latest;

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

        if (
            profile.LanguageVersion < LanguageVersion.Version1_2 &&
            expectedResultType is not null &&
            ContainsPrimitive(expectedResultType)
        )
        {
            throw new ArgumentException(
                "Type 'primitive' requires language version 1.2.",
                nameof(expectedResultType)
            );
        }

        SyntaxTree syntaxTree = Parser.Parse(
            source,
            compilationMode,
            profile
        );
        return Binder.Bind(
            syntaxTree,
            environment,
            expectedResultType
        );
    }

    private static SourceText CreateSourceText(
        string source,
        Uri? sourceDocument
    )
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (
            sourceDocument is not null &&
            (
                !sourceDocument.IsAbsoluteUri ||
                string.IsNullOrWhiteSpace(sourceDocument.OriginalString)
            )
        )
        {
            throw new ArgumentException(
                "The source document URI must be absolute.",
                nameof(sourceDocument)
            );
        }

        return SourceText.From(source);
    }

    private static bool ContainsPrimitive(TypeSymbol type)
    {
        return ContainsPrimitive(
            type,
            new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance)
        );
    }

    private static bool ContainsPrimitive(
        TypeSymbol type,
        ISet<TypeSymbol> visited
    )
    {
        if (!visited.Add(type))
        {
            return false;
        }

        return type switch
        {
            { Kind: TypeKind.Primitive } => true,
            NullableTypeSymbol nullable =>
                ContainsPrimitive(nullable.UnderlyingType, visited),
            ArrayTypeSymbol array => ContainsPrimitive(array.ElementType, visited),
            ObjectTypeSymbol objectType => objectType.Properties.Any(
                property => ContainsPrimitive(property.Type, visited)
            ),
            _ => false,
        };
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
                TokenKind.CatchKeyword or
                TokenKind.ContinueKeyword or
                TokenKind.ElseKeyword or
                TokenKind.FalseKeyword or
                TokenKind.FinallyKeyword or
                TokenKind.FloatKeyword or
                TokenKind.ForKeyword or
                TokenKind.FuncKeyword or
                TokenKind.IfKeyword or
                TokenKind.IntKeyword or
                TokenKind.NullKeyword or
                TokenKind.NumberKeyword or
                TokenKind.ObjectKeyword or
                TokenKind.PrimitiveKeyword or
                TokenKind.ReturnKeyword or
                TokenKind.StringKeyword or
                TokenKind.ThrowKeyword or
                TokenKind.TrueKeyword or
                TokenKind.TryKeyword or
                TokenKind.TypeKeyword or
                TokenKind.UnknownKeyword or
                TokenKind.VarKeyword or
                TokenKind.VoidKeyword or
                TokenKind.WhileKeyword => SourceClassificationKind.Keyword,
            _ => SourceClassificationKind.Punctuation,
        };
    }
}
