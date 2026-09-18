# Detailed changelog

This file records consumer-visible changes for stable, release-candidate, and beta versions. Alpha versions may be included when useful.

Changes are classified as:

- breaking changes;
- new features;
- fixes.

Stable entries describe the incremental change since the preceding prerelease. The main `CHANGELOG.md` provides the consolidated history between stable versions.

## Unreleased

## `0.1.0-alpha.2` - 2026-09-18

### Breaking changes

- Changed the numeric value of [`OpenObjectsFeature.Enabled`](https://fsttesla.github.io/MuLang/api/MuLang.Core.OpenObjectsFeature.html) from `1` to `2`.

### New features

- Added [`OpenObjectsFeature.PropertyExistenceOnly`](https://fsttesla.github.io/MuLang/api/MuLang.Core.OpenObjectsFeature.html#MuLang_Core_OpenObjectsFeature_PropertyExistenceOnly), which enables dynamic `has` tests without enabling open-object creation, access, mutation, or provider-declared open types. See the [open-object profile semantics](https://fsttesla.github.io/MuLang/language/18-language-profiles.html#185-open-objects) for details.
