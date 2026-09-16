# 17. Diagnostics

Compilation diagnostics are divided into:

- lexical diagnostics;
- syntax diagnostics;
- binding diagnostics;
- type diagnostics;
- control-flow diagnostics;
- exporter diagnostics.

Every diagnostic MUST contain:

- a stable code;
- a severity;
- a source span;
- a message.

The compiler SHOULD continue after recoverable errors to report multiple independent diagnostics.

Diagnostics for syntax or capabilities disabled by the selected language profile are defined in [Section 18.10](18-language-profiles.md#1810-diagnostics).

No executable artifact may be produced when error diagnostics are present.
