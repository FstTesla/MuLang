# 2. Source text and grammar notation

## 2.1. Source text

Source text is a sequence of Unicode scalar values.

The language is case-sensitive.

Diagnostics MUST identify the range of source text to which they apply.

## 2.2. Grammar notation

The grammar in this specification uses the following notation:

- quoted text denotes a token;
- `?` denotes an optional production;
- `*` denotes zero or more repetitions;
- `+` denotes one or more repetitions;
- `|` separates alternatives;
- parentheses group productions.

Where the precedence table and a grammar summary overlap, the precedence table determines expression grouping.
