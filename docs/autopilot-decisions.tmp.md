# Autopilot decisions

## 2. Initial compiler population

Populate function, parameter, local, provider-symbol, and type-name attributes. Reserve but do not populate `source-document` and `declaration-span`, because the compiler currently has no stable document identity and declaration-span propagation is optional in the plan.
