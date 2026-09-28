# 3. Lexical structure

## 3.1. Whitespace

Whitespace separates tokens and otherwise has no semantic meaning.

Whitespace consists of spaces, horizontal tabs, carriage returns, and line feeds.

## 3.2. Comments

Comments are not supported. Character sequences commonly used to introduce comments MUST be tokenized according to the normal operator and punctuation rules or reported as invalid tokens.

> Consequently, this source does not contain a comment and produces a lexical or syntax error:
>
> ```text
> var value = 1; // not a comment
> ```

## 3.3. Identifiers

An identifier starts with a Unicode letter or underscore and continues with Unicode letters, decimal digits, or underscores.

Identifiers are case-sensitive.

An identifier that exactly matches a reserved keyword cannot be used as an identifier.

> For example, `value`, `Value`, and `valüe2` are distinct identifiers, while `return` cannot be used as an identifier.

## 3.4. Reserved keywords

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

Language version 1.1 additionally reserves:

- `infty`
- `nan`

When compiling an earlier language version, each identifier occurrence whose spelling becomes reserved in a later supported version produces a warning while remaining an identifier.

Host-defined function, global, and type names MUST NOT use reserved keywords.

## 3.5. Boolean literals

The boolean literals are `true` and `false`.

## 3.6. Null literal

The null literal is `null`.

`null` is a value but is not a denotable type.

## 3.7. Integer literals

An integer literal consists of a non-empty sequence of decimal digits without a decimal separator or exponent. In a position where a literal is permitted, an immediately preceding `+` or `-` token is part of the signed literal.

Whitespace between the sign and numeric token has no semantic meaning.

The complete signed value MUST be representable as a signed 64-bit integer. A value outside that range is a compile-time error.

> These expressions are valid integer literals:
>
> ```text
> 0
> -9223372036854775808
> +42
> ```
>
> This literal produces a compile-time error because it is outside the `int` range:
>
> ```text
> 9223372036854775808
> ```

## 3.8. Float literals

A finite float literal consists of a numeric portion containing a decimal separator, an exponent, or both. In a position where a literal is permitted, an immediately preceding `+` or `-` token is part of the signed literal.

Float literals use `.` as the decimal separator and are independent of host culture.

The complete finite value MUST be representable as a finite IEEE 754 binary64 value. A numeric portion that overflows to infinity is a compile-time error.

Language version 1.1 additionally provides the `infty` and `nan` keyword literals for positive infinity and NaN. An immediately preceding `+` or `-` token is part of the literal under the same conditions as for finite numeric literals. `-infty` denotes negative infinity. Both signed forms of `nan` denote NaN without preserving a distinct sign.

Float values and operations use IEEE 754 binary64 semantics.

> These are `float` literals:
>
> ```text
> 1.0
> 6.022e23
> -infty
> nan
> ```

## 3.9. String literals

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

> For example, this literal contains a line feed and the Unicode scalar value U+1F680:
>
> ```text
> "first\nsecond \U0001F680"
> ```

## 3.10. Read-only array tokens

Language version 1.1 recognizes `$` as a postfix type-capability token and `$[` as the single opening token of a read-only array literal.

`$[` is one token and MUST NOT be split into `$` followed by `[`.

Language version 1 rejects both token forms as unavailable in that version.

> For example, language version 1.1 recognizes a read-only array type and literal in:
>
> ```text
> var values: int[]$ = $[1, 2, 3];
> ```
