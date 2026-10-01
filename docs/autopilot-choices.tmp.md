# Autonomous standard-library choices

This file records implementation choices that are not fixed explicitly by
`standard-library.plan.md` and should be reviewed before the `0.2.0-alpha.7`
release.

## Public API names

- Composition with host declarations accepts a completed `EnvironmentSchema`
  rather than an existing `EnvironmentBuilder`, because the builder does not
  expose its current declarations and therefore cannot guarantee cross-kind
  provider-identifier collision detection.
- The .NET implementation model uses `DotNetStandardLibraryModuleBinding`,
  `DotNetStandardLibraryComposition`, and `DotNetStandardLibraryComposer`.

## Stable identifiers

- Identifiers are declared directly by the canonical module objects and reused
  by the .NET bindings; no parallel identifier-constant catalog is maintained.
