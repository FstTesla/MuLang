# Roadmap

## Current state

Package release `0.3.0` is complete. MuLang `1.2` provides the complete
object-literal model, user-defined and inline structural object types,
`primitive`, and source-level exception handling. MuIR 2 provides lifetime and
exception regions, property mutability, source metadata, and well-known
diagnostic attributes while retaining MuIR 1 readability.

The compiler, .NET exporter, standard library, language server, Visual Studio
extension, specifications, API baselines, packages, and VSIX have completed the
stable release cycle. Language samples now separate valid programs, warnings,
and intentional semantic errors, and the MuIR serialization fixtures cover the
current format capabilities. Compiler-backed hover, document highlights,
definition navigation, workspace references, and dedicated property
classification are available through the language server. Visual Studio renders
the categorized hover model through native theme-aware Quick Info.

MuIR editor support is implemented for formats 1 and 2, with a dedicated
`.muir` content type and TextMate grammar, semantic highlighting, hover,
document-local definitions, references and occurrence highlights, document
symbols, and structural folding. Consultation is independent of MuLang source
compilation and host configuration; it does not publish diagnostics or validate
execution eligibility. Invalid documents retain lexical fallback without stale
semantic results.

## Goal: package release `0.4.0`

Use the stable MuLang `1.2` and MuIR 2 contracts as the foundation for a
tooling-focused release. Language syntax, semantics, standard profiles, and
the MuIR format remain frozen throughout the `0.4.0` cycle except for
release-blocking correctness fixes.

## Sequence

1. **Stabilize and release.** Validate the language server and VSIX across
   supported Visual Studio versions, retain cross-platform compiler and package
   validation, update editor documentation and samples, and publish beta and
   RC prereleases before tagging `0.4.0`.

## Follow-up and non-blocking work

Rename depends on stable reference discovery and may follow navigation and
references. Formatting requires a separately defined canonical source style.
Incremental parsing and incremental semantic-token responses are performance
optimizations rather than initial `0.4.0` release requirements.

MuIR live diagnostics remain deferred and may later reuse `MuIrReader`.
They may ship in `0.4.x` without blocking the first tooling release.

## Next release cycle after `0.4.0`

Introduce MuLang `1.3` with strict semicolon-free user-defined type declarations:
the declaration ends at its closing brace, and a following semicolon is rejected
rather than accepted as optional syntax. Preserve the required semicolon for
MuLang `1.2` compilations. Update the parser, diagnostics, language specification,
editor support, samples, and tests together. This syntax change belongs to the
next release cycle, not `0.4.x` servicing releases.

Add a feature-flag-controlled declaration-mutability mode for MuLang `1.3`.
When enabled, local variables and user-defined function parameters are read-only
by default; an explicit modifier makes their bindings read-write. Keep `$`
valid as an explicit, redundant read-only modifier in this mode. With the flag
disabled, preserve existing defaults: mutable locals and read-only parameters.

Choose the final modifier before implementation, with `%` as a candidate, and
define the flag, its defaults in standard profiles, and modifier availability
when the flag is disabled. Binding mutability is independent of object-property
and array-element capabilities; this work does
not change their defaults or host-global mutability. Read-only locals retain
the existing definite-assignment and single-assignment rules. Read-write
parameters permit rebinding inside the callee, not `ref`/`out` passing or
reassignment of the caller's binding.

Update declaration syntax, binding and flow analysis, profile validation and
fingerprints, diagnostics, specifications, editor configuration, highlighting,
hover, samples, and tests together. Preserve existing language-version behavior
and assess any necessary IR or exporter changes without assuming a format bump.

## Deferred beyond `0.4.0`

Add VS Code support through a dedicated language-client extension that reuses
the existing language server and shared TextMate grammars and language
configuration assets. Define configuration-file and editor-settings precedence,
cross-platform server distribution, packaging, and release integration without
duplicating compiler functionality or introducing editor dependencies into the
language server.

Completion, standard-library expansion, comments, first-class functions, and
function types remain potential future work and are not part of the tooling
release.
