# 17. Diagnostics

Compilation diagnostics are divided into:

- lexical diagnostics;
- syntax diagnostics;
- binding diagnostics;
- type diagnostics;
- control-flow diagnostics;
- constant-evaluation diagnostics;
- exporter diagnostics.

Every diagnostic MUST contain:

- a stable code;
- a severity;
- a source span;
- a message.

The compiler SHOULD continue after recoverable errors to report multiple independent diagnostics.

Diagnostics for syntax or capabilities disabled by the selected language profile are defined in [Section 18.11](18-language-profiles.md#1811-diagnostics).

No executable artifact may be produced when error diagnostics are present.

When compile-time constant folding is enabled, an operation that fails while evaluating a required constant expression is a compile-time error as defined in [Section 10.11](10-expressions.md#1011-compile-time-constant-evaluation).
