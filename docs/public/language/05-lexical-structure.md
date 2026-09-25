# 5. Lexical structure

## 5.1. Whitespace

Whitespace separates tokens and otherwise has no semantic meaning.

Whitespace consists of spaces, horizontal tabs, carriage returns, and line feeds.

## 5.2. Comments

Comments are not supported. Character sequences commonly used to introduce comments MUST be tokenized according to the normal operator and punctuation rules or reported as invalid tokens.

## 5.3. Identifiers

An identifier starts with a Unicode letter or underscore and continues with Unicode letters, decimal digits, or underscores.

Identifiers are case-sensitive.

An identifier that exactly matches a reserved keyword cannot be used as an identifier.

## 5.4. Reserved keywords

Language version 1 reserves:

- `as`
- `bool`
- `break`
- `continue`
- `else`
- `false`
- `float`
- `func`
- `for`
- `has`
- `if`
- `int`
- `is`
- `null`
- `number`
- `object`
- `return`
- `string`
- `true`
- `unknown`
- `var`
- `void`
- `while`

Provider-defined function, global, and type names MUST NOT use reserved keywords.

## 5.5. Boolean literals

The boolean literals are `true` and `false`.

## 5.6. Null literal

The null literal is `null`.

`null` is a value but is not a denotable type.

## 5.7. Integer literals

An integer literal consists of an optional leading `+` or `-` sign followed by a non-empty sequence of decimal digits without a decimal separator or exponent.

The lexer emits the sign and unsigned numeric portion as separate tokens. The parser combines them into one signed literal syntax node when a sign is followed by a numeric token in a position where a literal can occur.

Whitespace between the sign and numeric token has no semantic meaning.

The complete signed value MUST be representable as a signed 64-bit integer. A value outside that range is a compile-time error.

## 5.8. Float literals

A float literal consists of an optional leading `+` or `-` sign and a numeric portion containing a decimal separator, an exponent, or both.

Float literals use `.` as the decimal separator and are independent of host culture.

Float values use IEEE 754 binary64 semantics. The source syntax does not provide literals for NaN or infinity.

The lexer emits the sign and unsigned numeric portion as separate tokens. The parser combines them into one signed literal syntax node under the same rules as integer literals.

## 5.9. String literals

String literals are delimited by double quotes.

The supported escape sequences are:

- `\"` for a double quote;
- `\\` for a backslash;
- `\n` for a line feed;
- `\r` for a carriage return;
- `\t` for a horizontal tab;
- `\0` for the null character;
- `\uXXXX` for a four-hex-digit Unicode code unit;
- `\UXXXXXXXX` for an eight-hex-digit Unicode scalar value.

Unicode escapes MUST NOT encode an unpaired surrogate or a value outside the Unicode scalar range.

An invalid escape sequence or unterminated string is a lexical error.

String values are sequences of Unicode scalar values and are compared ordinally.

## 5.10. Read-only array tokens

Language version 1.1 recognizes `$` as a postfix type-capability token and `$[` as the single opening token of a read-only array literal.

The lexer MUST recognize `$[` before standalone `$`. Language version 1 recognizes both token shapes for recovery but reports that they require language version 1.1.
