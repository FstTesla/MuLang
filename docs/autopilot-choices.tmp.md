# Autopilot choices

## Roadmap scope

- Interpreted roadmap point 5 as the complete scoped-slot lifetime-region work.
- Interpreted roadmap point 6 as the end-to-end portable IR exception-region foundation described by the coordinated implementation sequence, excluding source `try`, `catch`, `throw`, and `finally` syntax, which the roadmap lists in later points.

## Lifetime-region lowering

- Preserve the existing public constructors and deconstruction shapes while adding explicit lifetime ownership through additive overloads and properties.
- Use parent-owned bridge blocks around nested lifetime regions. Loop back edges therefore leave the body region, pass through the loop condition or iterator in the parent region, and re-enter through the region entry.
- Assign compiler temporaries to the active lifetime region. Existing lowering creates merge-result temporaries before entering branch regions, so values used after a nested region naturally belong to the required ancestor.

## Exception-region execution

- Keep expression-tree compilation for functions without exception regions.
- Use an explicit portable control-flow interpreter for functions that contain exception regions. This avoids CLR `finally` semantics, so uncatchable MuLang errors bypass cleanup as required.
- Keep source exception syntax out of this release checkpoint because roadmap points 7 and later own parsing, binding, and lowering of `try`, `catch`, `throw`, and `finally`.
