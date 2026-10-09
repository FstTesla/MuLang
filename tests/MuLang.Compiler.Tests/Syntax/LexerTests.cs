using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.Compiler.Tests.Syntax;

public sealed class LexerTests
{
    [Test]
    public void RecognizesKeywordsAndUnicodeIdentifiers()
    {
        LexResult result = Lexer.Lex(SourceText.From("var café = true;"));

        Assert.That(
            result.Tokens.Select(static token => token.Kind),
            Is.EqualTo(
                [
                    TokenKind.VarKeyword,
                    TokenKind.Identifier,
                    TokenKind.Equal,
                    TokenKind.TrueKeyword,
                    TokenKind.Semicolon,
                    TokenKind.EndOfFile,
                ]
            )
        );
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void RecognizesFloatKeyword()
    {
        LexResult result = Lexer.Lex(SourceText.From("float"));

        Assert.That(result.Tokens[0].Kind, Is.EqualTo(TokenKind.FloatKeyword));
    }

    [Test]
    public void RecognizesNonFiniteFloatKeywordsInVersionOneOne()
    {
        LexResult result = Lexer.Lex(
            SourceText.From("infty nan"),
            LanguageProfiles.Version1_1
        );

        Assert.That(
            result.Tokens.Select(static token => token.Kind),
            Is.EqualTo(
                [
                    TokenKind.InftyKeyword,
                    TokenKind.NanKeyword,
                    TokenKind.EndOfFile,
                ]
            )
        );
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void WarnsForNonFiniteFloatIdentifiersInVersionOne()
    {
        LexResult result = Lexer.Lex(
            SourceText.From("infty nan"),
            LanguageProfiles.Version1
        );

        Assert.That(
            result.Tokens.Select(static token => token.Kind),
            Is.EqualTo(
                [
                    TokenKind.Identifier,
                    TokenKind.Identifier,
                    TokenKind.EndOfFile,
                ]
            )
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Is.EqualTo(
                    [
                        DiagnosticCodes.FutureReservedKeyword,
                        DiagnosticCodes.FutureReservedKeyword,
                    ]
                )
            );
            Assert.That(
                result.Diagnostics,
                Has.All.Property("Severity").EqualTo(DiagnosticSeverity.Warning)
            );
            Assert.That(result.Diagnostics.HasErrors, Is.False);
        }
    }

    [Test]
    public void VersionsPrimitiveKeyword()
    {
        LexResult earlier = Lexer.Lex(
            SourceText.From("primitive"),
            LanguageProfiles.Version1_1
        );
        LexResult current = Lexer.Lex(
            SourceText.From("primitive"),
            LanguageProfiles.Version1_2
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(earlier.Tokens[0].Kind, Is.EqualTo(TokenKind.Identifier));
            Assert.That(
                earlier.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.FutureReservedKeyword)
            );
            Assert.That(
                current.Tokens[0].Kind,
                Is.EqualTo(TokenKind.PrimitiveKeyword)
            );
            Assert.That(current.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void VersionsExceptionHandlingKeywords()
    {
        LexResult earlier = Lexer.Lex(
            SourceText.From("catch error finally throw try"),
            LanguageProfiles.Version1_1
        );
        LexResult current = Lexer.Lex(
            SourceText.From("catch error finally throw try"),
            LanguageProfiles.Version1_2
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                earlier.Tokens.Select(static token => token.Kind),
                Is.EqualTo(
                    [
                        TokenKind.Identifier,
                        TokenKind.Identifier,
                        TokenKind.Identifier,
                        TokenKind.Identifier,
                        TokenKind.Identifier,
                        TokenKind.EndOfFile,
                    ]
                )
            );
            Assert.That(
                earlier.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Is.EqualTo(
                    [
                        DiagnosticCodes.FutureReservedKeyword,
                        DiagnosticCodes.FutureReservedKeyword,
                        DiagnosticCodes.FutureReservedKeyword,
                        DiagnosticCodes.FutureReservedKeyword,
                        DiagnosticCodes.FutureReservedKeyword,
                    ]
                )
            );
            Assert.That(
                earlier.Diagnostics,
                Has.All.Property("Severity").EqualTo(DiagnosticSeverity.Warning)
            );
            Assert.That(earlier.Diagnostics.HasErrors, Is.False);
            Assert.That(
                current.Tokens.Select(static token => token.Kind),
                Is.EqualTo(
                    [
                        TokenKind.CatchKeyword,
                        TokenKind.ErrorKeyword,
                        TokenKind.FinallyKeyword,
                        TokenKind.ThrowKeyword,
                        TokenKind.TryKeyword,
                        TokenKind.EndOfFile,
                    ]
                )
            );
            Assert.That(current.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void VersionsTypeKeyword()
    {
        LexResult earlier = Lexer.Lex(
            SourceText.From("type"),
            LanguageProfiles.Version1_1
        );
        LexResult current = Lexer.Lex(
            SourceText.From("type"),
            LanguageProfiles.Version1_2
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(earlier.Tokens[0].Kind, Is.EqualTo(TokenKind.Identifier));
            Assert.That(
                earlier.Diagnostics.Single().Code,
                Is.EqualTo(DiagnosticCodes.FutureReservedKeyword)
            );
            Assert.That(current.Tokens[0].Kind, Is.EqualTo(TokenKind.TypeKeyword));
            Assert.That(current.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void RecognizesOperatorsUsingLongestMatch()
    {
        LexResult result = Lexer.Lex(
            SourceText.From("?. ?.[ ?: ?? @{ $[ $ === !== == != <= >= << >> && || ~"),
            LanguageProfiles.Version1_1
        );

        Assert.That(
            result.Tokens.Select(static token => token.Kind),
            Is.EqualTo(
                [
                    TokenKind.OptionalDot,
                    TokenKind.OptionalOpenBracket,
                    TokenKind.Question,
                    TokenKind.Colon,
                    TokenKind.QuestionQuestion,
                    TokenKind.OpenObjectBrace,
                    TokenKind.ReadOnlyOpenBracket,
                    TokenKind.Dollar,
                    TokenKind.EqualEqualEqual,
                    TokenKind.BangEqualEqual,
                    TokenKind.EqualEqual,
                    TokenKind.BangEqual,
                    TokenKind.LessThanOrEqual,
                    TokenKind.GreaterThanOrEqual,
                    TokenKind.LeftShift,
                    TokenKind.RightShift,
                    TokenKind.AmpersandAmpersand,
                    TokenKind.PipePipe,
                    TokenKind.Tilde,
                    TokenKind.EndOfFile,
                ]
            )
        );
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ReportsReadOnlyArraySyntaxInVersionOne()
    {
        LexResult result = Lexer.Lex(
            SourceText.From("$[1] int[]$"),
            LanguageProfiles.Version1
        );

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Is.EqualTo(
                [
                    DiagnosticCodes.UnsupportedLanguageVersionFeature,
                    DiagnosticCodes.UnsupportedLanguageVersionFeature,
                ]
            )
        );
    }

    [Test]
    public void UsesLatestLanguageVersionByDefault()
    {
        LexResult result = Lexer.Lex(SourceText.From("$[1] int[]$"));

        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void KeepsNumericSignsAsSeparateTokens()
    {
        LexResult result = Lexer.Lex(SourceText.From("-12 +3.5 2e-4"));

        Assert.That(
            result.Tokens.Select(static token => token.Kind),
            Is.EqualTo(
                [
                    TokenKind.Minus,
                    TokenKind.IntegerLiteral,
                    TokenKind.Plus,
                    TokenKind.NumberLiteral,
                    TokenKind.NumberLiteral,
                    TokenKind.EndOfFile,
                ]
            )
        );
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [TestCase("0b101", 2, 5)]
    [TestCase("0B101", 2, 5)]
    [TestCase("0o17", 2, 4)]
    [TestCase("0O17", 2, 4)]
    [TestCase("0x2a", 2, 4)]
    [TestCase("0X2A", 2, 4)]
    public void RecognizesPrefixedIntegerLiterals(
        string source,
        int start,
        int length
    )
    {
        LexResult result = Lexer.Lex(
            SourceText.From($"- {source}"),
            LanguageProfiles.Version1_1
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                result.Tokens.Select(static token => token.Kind),
                Is.EqualTo(
                    [
                        TokenKind.Minus,
                        TokenKind.IntegerLiteral,
                        TokenKind.EndOfFile,
                    ]
                )
            );
            Assert.That(result.Tokens[1].Span, Is.EqualTo(new TextSpan(start, length)));
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [TestCase("0b")]
    [TestCase("0o")]
    [TestCase("0x")]
    public void ReportsPrefixedIntegerWithoutDigits(string source)
    {
        LexResult result = Lexer.Lex(
            SourceText.From(source),
            LanguageProfiles.Version1_1
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Tokens[0].Kind, Is.EqualTo(TokenKind.IntegerLiteral));
            Assert.That(result.Tokens[0].Span, Is.EqualTo(new TextSpan(0, 2)));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(DiagnosticCodes.InvalidNumber));
            Assert.That(result.Diagnostics.Single().Span, Is.EqualTo(new TextSpan(0, 2)));
        }
    }

    [TestCase("0b102", 4, 1)]
    [TestCase("0o89", 2, 2)]
    [TestCase("0x1g", 3, 1)]
    public void ReportsInvalidPrefixedIntegerDigits(
        string source,
        int diagnosticStart,
        int diagnosticCount
    )
    {
        LexResult result = Lexer.Lex(
            SourceText.From(source),
            LanguageProfiles.Version1_1
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Tokens[0].Span, Is.EqualTo(new TextSpan(0, source.Length)));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(diagnosticCount));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.InvalidNumber));
            Assert.That(
                result.Diagnostics[0].Span,
                Is.EqualTo(new TextSpan(diagnosticStart, 1))
            );
        }
    }

    [TestCase("0b101")]
    [TestCase("0O17")]
    [TestCase("0x1g")]
    public void RejectsCompletePrefixedIntegerInVersionOne(string source)
    {
        LexResult result = Lexer.Lex(
            SourceText.From(source),
            LanguageProfiles.Version1
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Tokens[0].Kind, Is.EqualTo(TokenKind.IntegerLiteral));
            Assert.That(result.Tokens[0].Span, Is.EqualTo(new TextSpan(0, source.Length)));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(
                result.Diagnostics[0].Code,
                Is.EqualTo(DiagnosticCodes.UnsupportedLanguageVersionFeature)
            );
            Assert.That(
                result.Diagnostics[0].Span,
                Is.EqualTo(new TextSpan(0, source.Length))
            );
        }
    }

    [Test]
    public void DecodesStringEscapes()
    {
        LexResult result = Lexer.Lex(SourceText.From("\"a\\n\\u0062\\U0001F600\""));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Tokens[0].Kind, Is.EqualTo(TokenKind.StringLiteral));
            Assert.That(result.Tokens[0].Value, Is.EqualTo("a\nb😀"));
            Assert.That(result.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsInvalidCharactersUsingScalarSpans()
    {
        LexResult result = Lexer.Lex(SourceText.From("😀"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Tokens[0].Kind, Is.EqualTo(TokenKind.Bad));
            Assert.That(result.Tokens[0].Span, Is.EqualTo(new TextSpan(0, 1)));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.InvalidCharacter));
        }
    }

    [Test]
    public void ReportsInvalidExponent()
    {
        LexResult result = Lexer.Lex(SourceText.From("1e+"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Tokens[0].Kind, Is.EqualTo(TokenKind.NumberLiteral));
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.InvalidNumber));
        }
    }

    [Test]
    public void ReportsUnterminatedStringWithoutConsumingNextLine()
    {
        LexResult result = Lexer.Lex(SourceText.From("\"text\nvar"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Diagnostics[0].Code, Is.EqualTo(DiagnosticCodes.UnterminatedString));
            Assert.That(result.Tokens[1].Kind, Is.EqualTo(TokenKind.VarKeyword));
        }
    }
}
