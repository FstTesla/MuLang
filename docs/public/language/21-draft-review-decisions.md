# 21. Draft review decisions

The following choices are made by this draft and require explicit review before the specification is considered stable:

1. Local declarations use `var`, with optional `: Type` annotation.
2. Global bindings are read-only, while their object or array contents can be mutable.
3. Arrays are invariant because they are mutable.
4. String concatenation implicitly converts primitive and null operands when the other operand is a string.
5. Removing an absent removable property is a runtime error rather than a no-op.
6. Empty statements are supported.
7. `for` supports one initializer and one iterator operation rather than comma-separated lists.
8. Explicit checked conversions use the left-associative `as` operator and fail with a runtime error.
9. Optional property access uses `?.`, optional element access uses `?.[`, and absent dynamic properties produce `null`.
10. Prefix `~` is bitwise complement, while postfix `~` is a property-removal statement.
11. Local variables cannot shadow visible local or global variables.
12. Open object literals use `@{`, while closed object literals use `{`.
13. Arrays expose a read-only intrinsic `length` property that is not an object property.
14. Checked conversion success and runtime mutation capabilities cannot always be queried before performing the corresponding operation.
15. Null coalescing uses C# precedence and associativity, requires a nullable left operand, and derives its result from the non-null left type and the right operand.
