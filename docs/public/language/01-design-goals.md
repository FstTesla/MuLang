# 1. Design goals

MuLang is a small, embeddable language for validating and executing expressions and imperative programs supplied as text.

The language is designed to:

- support expression-only and statement-based compilation;
- use a small, explicitly defined type system;
- obtain all global variables, functions, and structured types from a provider;
- prevent implicit access to runtime-specific APIs;
- produce deterministic diagnostics before execution whenever possible;
- preserve the same language semantics across future runtime exporters;
- allow controlled mutation of provider-supplied objects and arrays;
- remain suitable for execution with cancellation and resource limits.

MuLang uses C# as its primary reference for lexical conventions, expressions, statements, operator precedence, and control flow. Differences from C# are defined by this specification and are intentional.
