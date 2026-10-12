# Autopilot decisions

- `TextLineMap` stores the source length and line-start scalar offsets, and performs both offset-to-position and position-to-offset conversion without retaining source text.
- `TextLocation` is materialized and validates that its start and end offsets match its `TextSpan`.
- The compiler always emits a line map; `SourceDocument` remains optional and is supplied through additive `Analyze` and `Compile` overloads.
- MuIR 2 serializes the line map as an optional program diagnostic attribute so older readers can ignore it.
- Runtime errors are enriched only when their location is absent, preserving locations produced by nested programs or providers.
- Runtime stack-frame locations are materialized from the map belonging to the program that contributes each frame.
- The additive `Analyze` and `Compile` overloads suppress RS0027 locally because preserving the existing optional-parameter signatures is required for source and binary compatibility.
