using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core;
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
    public void RecognizesOperatorsUsingLongestMatch()
    {
        LexResult result = Lexer.Lex(
            SourceText.From("?. ?.[ ?: ?? @{ $[ $ === !== == != <= >= << >> && || ~"),
            LanguageProfiles.Version2
        );

        Assert.That(
            result.Tokens.Select(static token => token.Kind),
            Is.EqualTo(
                [
                    TokenKind.OptionalDot,
                    TokenKind.OptionalOpenBracket,
                    TokenKind.OptionalPropertyColon,
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
    public void UsesLanguageVersionTwoByDefault()
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
