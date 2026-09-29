# Autopilot choices for semantic warnings

The following implementation choices were made autonomously and should be reviewed:

1. New public diagnostic codes occupy `MUL3032` through `MUL3040`, following the existing binding and constant-evaluation diagnostics.
2. Warning analysis runs during binding and is independent of the `ConstantFoldingFeature` profile setting.
3. Constant warning facts reuse primitive runtime operations. Failures such as division by zero are ignored by warning analysis and retain their existing compile-time or runtime behavior.
4. Warning analysis uses static types and pure constant expressions but does not add flow-sensitive local-variable narrowing.
5. The existing validity rule for `??` is preserved. A statically non-nullable left operand remains invalid rather than becoming valid with a warning.
6. A source semicolon parsed as an empty statement is warned unless it is used directly as a control-statement body. Empty statements synthesized from another token during parser recovery are excluded.
7. Constant logical expressions receive one warning for the complete expression when its result is known. Otherwise, individually constant `&&` or `||` operands are warned. Duplicate warnings with the same code and source span are suppressed.
8. Optional access on a non-null target is considered redundant only for required properties, array indexing, and array `length`. It is retained without warning when it can handle an absent optional or dynamic property.
9. An optional access target known to be null is warned even when the selected property could be absent, because the access result is always null.
10. An always-true type test is detected when the source type is assignable to the tested type and the pair is valid for runtime conformance.
11. A cast is considered redundant only when its source and target types are equivalent. Statically successful casts that change the expression's static type are not warned.
12. Null-comparison warnings apply only when one operand is the `null` literal type and the other operand's nullness is statically determined.
