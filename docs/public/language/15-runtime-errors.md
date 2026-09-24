# 15. Runtime errors

A runtime failure MUST be represented as a MuLang runtime error containing:

- a stable error code;
- a message;
- the source span of the operation;
- an optional runtime-specific inner exception.

Runtime errors include:

- null operand where a non-null value is required;
- failed checked cast;
- integer overflow;
- division or remainder by integer zero;
- invalid shift count;
- invalid array index;
- absent property access;
- prohibited property or array mutation;
- prohibited property removal;
- incompatible runtime environment;
- exhausted execution budget;
- cancellation;
- provider function failure.
- exhausted user-function call depth.

Provider exceptions MUST be wrapped while preserving the original exception as the inner cause where the host runtime supports it.

The language provides no source-level mechanism for catching runtime errors.

## 15.1. Intentionally non-preventable runtime errors

Property assignment, array element assignment, or property removal can be rejected by the runtime adapter even though no source-level capability predicate is available.

A semantic checked-cast failure is preventable by testing the same unchanged value with the corresponding `is` expression. Operational failures encountered while evaluating either operation remain possible.

Provider failures, environment incompatibility, cancellation, and budget exhaustion are operational failures controlled outside the source program and are not considered semantic check gaps.
