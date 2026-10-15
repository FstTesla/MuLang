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
current format capabilities. The `0.4.0` language-server work has started with
per-workspace-folder configuration for compilation mode, profiles, declarative
host environments, and expected result types.

## Goal: package release `0.4.0`

Use the stable MuLang `1.2` and MuIR 2 contracts as the foundation for a
tooling-focused release. Language syntax, semantics, standard profiles, and
the MuIR format remain frozen throughout the `0.4.0` cycle except for
release-blocking correctness fixes.

## Sequence

1. **Complete and smoke-test language-server workspace configuration.** The
   implementation allows a workspace to select the compilation mode, standard
   profile with feature overrides, declarative host environment, and expected
   result type per workspace folder. The server uses standard LSP configuration
   requests with a notification fallback, preserves the last valid settings
   after invalid updates, and keeps standalone-file defaults. Before proceeding,
   install the generated VSIX and verify configuration reloads, diagnostics, and
   semantic tokens in Visual Studio.
2. **Build semantic editor features on the configured compiler model.** Add
   hover first, followed by definition navigation and references. Reuse compiler
   symbols, source spans, and diagnostic-name infrastructure rather than
   introducing editor-only semantic models.
3. **Integrate the capabilities through the VSIX.** Extend the existing
   language client and content-type registration without adding Visual
   Studio dependencies to the compiler or language server. Preserve TextMate
   highlighting as the immediate lexical fallback.
4. **Add lexical MuIR editor support.** Register `.muir` as a separate content
   type with its own TextMate grammar for comments, strings, numbers, document
   sections, types, instructions, terminators, attributes, and `t`, `%`, `bb`,
   `lr`, and `er` references. Do not route MuIR documents through the MuLang
   source compiler.
5. **Stabilize and release.** Validate the language server and VSIX across
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

## Deferred beyond `0.4.0`

Completion, standard-library expansion, comments, first-class functions, and
function types remain potential future work and are not part of the tooling
release.
