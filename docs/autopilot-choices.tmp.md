# Autopilot choices for review

## `infty` and `nan` literals

- `+nan` and `-nan` both bind directly to `double.NaN`. MuLang does not preserve or expose a NaN sign or payload.

## MuIR v1

- The wire grammar uses explicit section and collection counts. Canonical output remains line-oriented, while the reader treats ordinary whitespace and comments as insignificant.
- Type definitions are dependency-first and deduplicated by in-memory type identity. This guarantees backward-only type references without conflating distinct structured type instances that happen to be equivalent.
- Reader syntax diagnostics are fail-fast: malformed input returns no partial `IrProgram` and one precise, location-aware error. The public diagnostic collection keeps the API open to bounded recovery in a later format reader.
- One complete canonical fixture covers the multi-function, full-instruction, type, and constant compatibility cases in addition to the separate minimal fixture, instead of duplicating the same wire content across four files.
- Deserialization remains environment-independent. No convenience overload performs `IrValidator` validation because that would couple structural reading to host-selected environment policy.
