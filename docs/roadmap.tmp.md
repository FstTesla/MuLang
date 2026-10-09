# Roadmap

## Goal: package release `0.3.0`

The release completes the current `0.3.0` prerelease line. MuLang `1.2`
introduces the complete object-literal model together with user-defined object
types, and MuIR 2 adds well-known diagnostic attributes.

## Sequence

1. **Complete the MuLang `1.2` object model.** Stabilize the existing
   `0.3.0` prerelease features and complete user-defined object declarations
   and inline object types in
   [the implementation plan](user-defined-object-types.plan.md), building on
   the completed read-only and optional object-property model. Validate
   compiler, profile, runtime, IR projection, language-server, and
   documentation behavior.
2. **Add well-known IR attributes to MuIR 2.** Follow
   [the IR attributes plan](ir-attributes.plan.md), preserving MuIR 1
   compatibility. Keep attributes non-semantic and ensure type names survive
   structural projection.
3. **Stabilize the release candidate.** Run cross-platform build, test,
   package, and VSIX validation; resolve release-blocking regressions; update
   specifications, API baselines, and release notes. Publish beta and RC
   prereleases before tagging `0.3.0`.

## Parallel, non-blocking work

Add language-server workspace configuration for the language profile, host
environment, and expected result type when it can be completed without delaying
the release sequence. Completion, hover, navigation, rename, and formatting
remain outside the `0.3.0` release gate.

## Deferred beyond `0.3.0`

Standard-library expansion, comments, first-class functions, and function
types are deferred to later dedicated work.
