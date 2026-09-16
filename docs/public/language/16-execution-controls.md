# 16. Execution controls

Execution MUST support cancellation.

Execution MUST support a configurable budget.

The .NET runtime represents an unlimited budget with a null execution-budget value. When a budget is present, each instruction, terminator, provider call, and deeply traversed value currently has a fixed unit cost.

The budget is charged for executed portable IR instructions, provider function calls, user-defined function calls, and deep value traversal. Implementations MAY assign different fixed costs to different instruction categories, but the cost model MUST be deterministic for a given language version and profile.

Budget exhaustion produces a runtime error.

Cancellation MUST be observed at deterministic safe points, including loop back-edges, provider function boundaries, and user-defined function boundaries.

The .NET runtime MUST enforce a configurable maximum active user-function call depth. The top-level program and provider calls do not count toward this limit.
