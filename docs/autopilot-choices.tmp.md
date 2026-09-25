# Autopilot choices for review

## Recursive type graphs

- Recursive cycles must contain at least one structured-object node. Cycles composed only of nullable and array wrappers are rejected because they cannot be materialized as finite `TypeSymbol` objects.
- `ObjectTypeGraphBuilder` permits separate declarations with the same provider ID or language name so it can materialize arbitrary MuIR graphs. `EnvironmentBuilder` remains the authority that rejects duplicate registered provider IDs and names.