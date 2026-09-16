# 15. Runtime errors

A runtime failure MUST be represented as a MuLang runtime error containing:

- a stable error code;
- a message;
- the source span of the operation;
- an optional runtime-specific inner exception.

Runtime errors include:

- null operand where a non-null value is required;
- failed checked conversion;
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

The first language version intentionally permits two categories of data-dependent runtime failure that source code cannot always prevent through a prior check:

- a checked `as` conversion can fail even though no general convertibility predicate is available;
- a property assignment, array element assignment, or property removal can be rejected by the runtime adapter even though no source-level capability predicate is available.

These limitations are part of the first-version language contract rather than omissions in static validation.

Provider failures, environment incompatibility, cancellation, and budget exhaustion are operational failures controlled outside the source program and are not considered semantic check gaps.
