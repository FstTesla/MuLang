# Roadmap

## Goal: package release `0.3.0`

The release completes the current `0.3.0` prerelease line. MuLang `1.2`
introduces the complete object-literal model together with user-defined object
types, and MuIR 2 adds well-known diagnostic attributes.

## Sequence

1. **Complete the MuLang `1.2` object model.** Completed for
   `0.3.0-alpha.6`: source-declared and inline structural object types now
   build on the read-only and optional object-property model across compiler,
   profile, IR projection, language-server, VSIX, and specification surfaces.
2. **Add well-known IR attributes to MuIR 2.** Completed for
   `0.3.0-alpha.6`: standardized non-semantic program, function, slot, and type
   attributes preserve diagnostic names through structural projection and
   MuIR round trips while retaining MuIR 1 readability. Earlier prerelease
   MuIR 2 layouts are intentionally rejected.
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
