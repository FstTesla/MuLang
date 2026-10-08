# Autonomous implementation choices

## Exception-handling profile option

Add a dedicated `ExceptionHandlingFeature` language-profile option. The standard
MuLang 1.2 profile enables it, while the standard version 1 and 1.1 profiles
disable it. The option is dormant before language version 1.2, and the built-in
`error` type remains available in 1.2 independently from the option.

This resolves the plan's non-definitive wording while preserving the explicit
separation between language-version availability and configurable source
syntax.

## Delivery scope

Implement `try`, `catch`, `finally`, explicit `throw`, and rethrow together for
`0.3.0-alpha.4`. Although the recommended roadmap permits staging `finally`
after the initial handler model, the requested objective is implementation of
the complete source-level plan and the portable IR foundation is already
available.

## Changelog placement

Record the prerelease delta in `CHANGELOG.detailed.md` with base
`0.3.0-alpha.3`. Do not add an incremental alpha entry to `CHANGELOG.md`,
because that file explicitly contains consolidated stable-version changes.

## API baselines and compatibility suppressions

Record the new profile and diagnostic APIs in the corresponding
`PublicAPI.Unshipped.txt` files. Leave `PublicAPI.Shipped.txt` unchanged because
the additions first ship in `0.3.0-alpha.4`.

Leave compatibility suppression files unchanged. The implementation adds
public APIs without removing or changing shipped signatures, so suppressing an
API compatibility diagnostic would hide an unexpected regression rather than
document an intentional break.
