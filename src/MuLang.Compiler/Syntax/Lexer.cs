using MuLang.Compiler.Diagnostics;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;
using System.Globalization;
using System.Text;

namespace MuLang.Compiler.Syntax;

internal sealed class Lexer
{
    private readonly SourceText source;
    private readonly IList<SyntaxToken> tokens = [ ];
    private readonly IList<Diagnostic> diagnostics = [ ];
    private int position;

    private Lexer(SourceText source)
    {
        this.source = source;
    }

    public static LexResult Lex(SourceText source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lexer = new Lexer(source);
        lexer.LexTokens();

        return new LexResult(
            lexer.tokens.AsReadOnly(),
            DiagnosticCollection.Create(lexer.diagnostics)
        );
    }

    private bool IsAtEnd => position >= source.Length;

    private Rune Current => Peek(0);

    private void LexTokens()
    {
        while (!IsAtEnd)
        {
            if (IsWhitespace(Current))
            {
                ReadWhitespace();
                continue;
            }

            tokens.Add(ReadToken());
        }

        tokens.Add(new SyntaxToken(TokenKind.EndOfFile, new TextSpan(position, 0)));
    }

    private SyntaxToken ReadToken()
    {
        if (IsIdentifierStart(Current))
        {
            return ReadIdentifierOrKeyword();
        }

        if (IsDecimalDigit(Current))
        {
            return ReadNumber();
        }

        if (Current.Value == '"')
        {
            return ReadString();
        }

        var start = position;

        if (TryRead("@{"))
        {
            return CreateToken(TokenKind.OpenObjectBrace, start);
        }

        if (TryRead("?."))
        {
            return CreateToken(TokenKind.OptionalDot, start);
        }

        if (TryRead("?["))
        {
            return CreateToken(TokenKind.OptionalOpenBracket, start);
        }

        if (TryRead("==="))
        {
            return CreateToken(TokenKind.EqualEqualEqual, start);
        }

        if (TryRead("!=="))
        {
            return CreateToken(TokenKind.BangEqualEqual, start);
        }

        if (TryRead("=="))
        {
            return CreateToken(TokenKind.EqualEqual, start);
        }

        if (TryRead("!="))
        {
            return CreateToken(TokenKind.BangEqual, start);
        }

        if (TryRead("<="))
        {
            return CreateToken(TokenKind.LessThanOrEqual, start);
        }

        if (TryRead(">="))
        {
            return CreateToken(TokenKind.GreaterThanOrEqual, start);
        }

        if (TryRead("<<"))
        {
            return CreateToken(TokenKind.LeftShift, start);
        }

        if (TryRead(">>"))
        {
            return CreateToken(TokenKind.RightShift, start);
        }

        if (TryRead("&&"))
        {
            return CreateToken(TokenKind.AmpersandAmpersand, start);
        }

        if (TryRead("||"))
        {
            return CreateToken(TokenKind.PipePipe, start);
        }

        var kind = Current.Value switch
        {
            '(' => TokenKind.OpenParenthesis,
            ')' => TokenKind.CloseParenthesis,
            '{' => TokenKind.OpenBrace,
            '}' => TokenKind.CloseBrace,
            '[' => TokenKind.OpenBracket,
            ']' => TokenKind.CloseBracket,
            ',' => TokenKind.Comma,
            '.' => TokenKind.Dot,
            ':' => TokenKind.Colon,
            ';' => TokenKind.Semicolon,
            '?' => TokenKind.Question,
            '+' => TokenKind.Plus,
            '-' => TokenKind.Minus,
            '*' => TokenKind.Asterisk,
            '/' => TokenKind.Slash,
            '%' => TokenKind.Percent,
            '<' => TokenKind.LessThan,
            '>' => TokenKind.GreaterThan,
            '=' => TokenKind.Equal,
            '!' => TokenKind.Bang,
            '&' => TokenKind.Ampersand,
            '^' => TokenKind.Caret,
            '|' => TokenKind.Pipe,
            '~' => TokenKind.Tilde,
            _ => TokenKind.Bad,
        };

        position++;

        if (kind == TokenKind.Bad)
        {
            diagnostics.Add(
                new Diagnostic(
                    DiagnosticCodes.InvalidCharacter,
                    DiagnosticSeverity.Error,
                    DiagnosticCategory.Lexical,
                    new TextSpan(start, 1),
                    $"Invalid character '{source.GetText(new TextSpan(start, 1))}'."
                )
            );
        }

        return CreateToken(kind, start);
    }

    private SyntaxToken ReadIdentifierOrKeyword()
    {
        var start = position;
        position++;

        while (!IsAtEnd && IsIdentifierPart(Current))
        {
            position++;
        }

        var span = TextSpan.FromBounds(start, position);
        var text = source.GetText(span);

        return new SyntaxToken(GetKeywordKind(text), span);
    }

    private SyntaxToken ReadNumber()
    {
        var start = position;
        ReadDecimalDigits();

        var isNumber = false;
        if (!IsAtEnd && Current.Value == '.' && IsDecimalDigit(Peek(1)))
        {
            isNumber = true;
            position++;
            ReadDecimalDigits();
        }

        if (!IsAtEnd && Current.Value is 'e' or 'E')
        {
            isNumber = true;
            position++;

            if (!IsAtEnd && Current.Value is '+' or '-')
            {
                position++;
            }

            var exponentStart = position;
            ReadDecimalDigits();

            if (position == exponentStart)
            {
                diagnostics.Add(
                    new Diagnostic(
                        DiagnosticCodes.InvalidNumber,
                        DiagnosticSeverity.Error,
                        DiagnosticCategory.Lexical,
                        TextSpan.FromBounds(start, position),
                        "A number exponent requires at least one decimal digit."
                    )
                );
            }
        }

        return new SyntaxToken(
            isNumber ? TokenKind.NumberLiteral : TokenKind.IntegerLiteral,
            TextSpan.FromBounds(start, position)
        );
    }

    private SyntaxToken ReadString()
    {
        var start = position;
        var value = new StringBuilder();
        position++;
        var isTerminated = false;

        while (!IsAtEnd)
        {
            if (Current.Value == '"')
            {
                position++;
                isTerminated = true;
                break;
            }

            if (Current.Value is '\r' or '\n')
            {
                break;
            }

            if (Current.Value == '\\')
            {
                ReadEscape(value);
                continue;
            }

            value.Append(Current.ToString());
            position++;
        }

        if (!isTerminated)
        {
            diagnostics.Add(
                new Diagnostic(
                    DiagnosticCodes.UnterminatedString,
                    DiagnosticSeverity.Error,
                    DiagnosticCategory.Lexical,
                    TextSpan.FromBounds(start, position),
                    "String literal is not terminated."
                )
            );
        }

        return new SyntaxToken(
            TokenKind.StringLiteral,
            TextSpan.FromBounds(start, position),
            value.ToString()
        );
    }

    private void ReadEscape(StringBuilder value)
    {
        var start = position;
        position++;

        if (IsAtEnd)
        {
            ReportInvalidEscape(start);
            return;
        }

        var escapedValue = Current.Value switch
        {
            '"' => "\"",
            '\\' => "\\",
            'n' => "\n",
            'r' => "\r",
            't' => "\t",
            '0' => "\0",
            _ => null,
        };

        if (escapedValue is not null)
        {
            value.Append(escapedValue);
            position++;
            return;
        }

        if (Current.Value == 'u')
        {
            position++;
            ReadUnicodeEscape(value, start, 4);
            return;
        }

        if (Current.Value == 'U')
        {
            position++;
            ReadUnicodeEscape(value, start, 8);
            return;
        }

        position++;
        ReportInvalidEscape(start);
    }

    private void ReadUnicodeEscape(StringBuilder value, int start, int digitCount)
    {
        var scalarValue = 0;

        for (var index = 0; index < digitCount; index++)
        {
            if (IsAtEnd || !TryGetHexValue(Current, out var hexValue))
            {
                ReportInvalidEscape(start);
                return;
            }

            scalarValue = checked(scalarValue * 16 + hexValue);
            position++;
        }

        if (!Rune.IsValid(scalarValue) || scalarValue is >= 0xD800 and <= 0xDFFF)
        {
            ReportInvalidEscape(start);
            return;
        }

        value.Append(new Rune(scalarValue).ToString());
    }

    private void ReportInvalidEscape(int start)
    {
        diagnostics.Add(
            new Diagnostic(
                DiagnosticCodes.InvalidEscapeSequence,
                DiagnosticSeverity.Error,
                DiagnosticCategory.Lexical,
                TextSpan.FromBounds(start, position),
                "Invalid string escape sequence."
            )
        );
    }

    private void ReadWhitespace()
    {
        do
        {
            position++;
        }
        while (!IsAtEnd && IsWhitespace(Current));
    }

    private void ReadDecimalDigits()
    {
        while (!IsAtEnd && IsDecimalDigit(Current))
        {
            position++;
        }
    }

    private bool TryRead(string text)
    {
        if (position + text.Length > source.Length)
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (Peek(index).Value != text[index])
            {
                return false;
            }
        }

        position += text.Length;
        return true;
    }

    private Rune Peek(int offset)
    {
        var target = position + offset;

        return target >= 0 && target < source.Length
            ? source.GetRune(target)
            : default;
    }

    private SyntaxToken CreateToken(TokenKind kind, int start)
    {
        return new SyntaxToken(kind, TextSpan.FromBounds(start, position));
    }

    private static TokenKind GetKeywordKind(string text)
    {
        return text switch
        {
            "as" => TokenKind.AsKeyword,
            "bool" => TokenKind.BoolKeyword,
            "break" => TokenKind.BreakKeyword,
            "continue" => TokenKind.ContinueKeyword,
            "else" => TokenKind.ElseKeyword,
            "false" => TokenKind.FalseKeyword,
            "for" => TokenKind.ForKeyword,
            "has" => TokenKind.HasKeyword,
            "if" => TokenKind.IfKeyword,
            "int" => TokenKind.IntKeyword,
            "is" => TokenKind.IsKeyword,
            "null" => TokenKind.NullKeyword,
            "number" => TokenKind.NumberKeyword,
            "object" => TokenKind.ObjectKeyword,
            "return" => TokenKind.ReturnKeyword,
            "string" => TokenKind.StringKeyword,
            "true" => TokenKind.TrueKeyword,
            "unknown" => TokenKind.UnknownKeyword,
            "var" => TokenKind.VarKeyword,
            "void" => TokenKind.VoidKeyword,
            "while" => TokenKind.WhileKeyword,
            _ => TokenKind.Identifier,
        };
    }

    private static bool IsWhitespace(Rune rune)
    {
        return rune.Value is ' ' or '\t' or '\r' or '\n';
    }

    private static bool IsIdentifierStart(Rune rune)
    {
        return rune.Value == '_' || Rune.IsLetter(rune);
    }

    private static bool IsIdentifierPart(Rune rune)
    {
        return IsIdentifierStart(rune) || IsDecimalDigit(rune);
    }

    private static bool IsDecimalDigit(Rune rune)
    {
        return Rune.GetUnicodeCategory(rune) == UnicodeCategory.DecimalDigitNumber;
    }

    private static bool TryGetHexValue(Rune rune, out int value)
    {
        value = rune.Value switch
        {
            >= '0' and <= '9' => rune.Value - '0',
            >= 'a' and <= 'f' => rune.Value - 'a' + 10,
            >= 'A' and <= 'F' => rune.Value - 'A' + 10,
            _ => -1,
        };

        return value >= 0;
    }
}
