# Autopilot decisions

## 1. Language version

Implement user-defined object types in MuLang 1.2 rather than introducing 1.3. The plan status and fixed decisions target 1.2, and the repository already uses 1.2 as the current latest profile for the `0.3.0` prerelease line.

## 2. Syntax representation

Refactor `TypeSyntax` into a primary-type node plus the existing ordered suffix tokens. Use the same `ObjectTypeBodySyntax` node and parser for named declarations and inline types so property grammar, recovery, and diagnostics remain consistent.

## 3. Type identity and lowering

Represent named source declarations with source-named `ObjectTypeSymbol` instances that have no provider identifier. Keep structural type relations and portable IR projection unchanged so source names are descriptive only and are erased during lowering.

## 4. Release scope

Treat `0.3.0-alpha.6` as the next incremental prerelease after `0.3.0-alpha.5`. Update the detailed changelog and all API/compatibility baselines affected by the new public profile setting, without changing the consolidated stable changelog unless repository validation identifies a release-process requirement.

## 5. Diagnostic allocation

Use syntax diagnostics `MUL2015` through `MUL2017`, binding diagnostics `MUL3049` through `MUL3051`, and feature diagnostics `MUL7015` and `MUL7016`. This preserves the repository's existing diagnostic category ranges and keeps named declarations separately suppressible from inline object types.

## 6. Compatibility baselines

Place the new profile APIs and diagnostic constants in `PublicAPI.Unshipped.txt`; do not move them to shipped baselines before the `0.3.0-alpha.6` package is published. Keep compatibility suppression files unchanged unless package validation reports a concrete compatibility break requiring a suppression.
