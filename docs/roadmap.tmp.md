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

## Goal: package release `0.4.0`

Use the stable MuLang `1.2` and MuIR 2 contracts as the foundation for a
tooling-focused release. Language syntax, semantics, standard profiles, and
the MuIR format remain frozen throughout the `0.4.0` cycle except for
release-blocking correctness fixes.

## Sequence

1. **Add lexical MuIR editor support.** Register `.muir` as a separate content
   type with its own TextMate grammar for comments, strings, numbers, document
   sections, types, instructions, terminators, attributes, and `t`, `%`, `bb`,
   `lr`, and `er` references. Do not route MuIR documents through the MuLang
   source compiler.
2. **Stabilize and release.** Validate the language server and VSIX across
   supported Visual Studio versions, retain cross-platform compiler and package
   validation, update editor documentation and samples, and publish beta and
   RC prereleases before tagging `0.4.0`.

## Follow-up and non-blocking work

Rename depends on stable reference discovery and may follow navigation and
references. Formatting requires a separately defined canonical source style.
Incremental parsing and incremental semantic-token responses are performance
optimizations rather than initial `0.4.0` release requirements.

MuIR live diagnostics may later reuse `MuIrReader`, with semantic highlighting
and navigation between slots, blocks, lifetime regions, and exception regions
added only after the independent lexical content type is established. These
features may ship in `0.4.x` without blocking the first tooling release.

## Next release cycle after `0.4.0`

Introduce MuLang `1.3` with strict semicolon-free user-defined type declarations:
the declaration ends at its closing brace, and a following semicolon is rejected
rather than accepted as optional syntax. Preserve the required semicolon for
MuLang `1.2` compilations. Update the parser, diagnostics, language specification,
editor support, samples, and tests together. This syntax change belongs to the
next release cycle, not `0.4.x` servicing releases.

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
