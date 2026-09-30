# 13. Execution controls

Execution MUST support cancellation.

Execution MUST support a configurable resource budget and a configurable maximum active user-function call depth.

The resource budget limits work performed by source evaluation, function calls, and recursive validation or traversal of values. Exhausting the budget produces a runtime error.

Runtime services exposed to provider functions participate in the same resource
budget. Array reads, object-property reads or enumeration, and structural
operations MUST NOT provide an unmetered path around execution controls.

The exact accounting units are implementation-defined. For a given implementation, language version, profile, source program, input values, and host-function behavior, accounting MUST be deterministic.

Cancellation MUST be observed at deterministic safe points frequently enough to interrupt loops, calls, and deep value traversal. Observing cancellation produces a runtime error.

Provider functions receive the execution cancellation state through their
invocation context and SHOULD observe it during long-running host operations.

The call-depth limit counts active user-defined function invocations. The top-level program and host-provided function calls do not count toward the limit. Exceeding the limit produces a runtime error.

> For example, this program is valid source but eventually produces a call-depth runtime error unless execution is cancelled or its resource budget is exhausted first:
>
> ```text
> func recurse(): int {
>     return recurse();
> }
> return recurse();
> ```
>
> Similarly, this loop can be interrupted by cancellation or budget exhaustion:
>
> ```text
> while (true) {
> }
> ```
