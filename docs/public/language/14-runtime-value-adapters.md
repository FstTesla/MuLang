# 14. Runtime value adapters

Runtime-specific values MUST be accessed through explicit adapters rather than implicit reflection.

The .NET runtime accepts object and array values only through its explicit object and array adapter interfaces. CLR dictionaries, lists, arrays, POCOs, and other host values are not recognized implicitly.

Global values and provider function results are recursively validated against their declared MuLang types when they cross the runtime boundary.

Deep runtime traversal is subject to the execution budget and to a configurable maximum traversal depth.

Adapters define:

- property lookup;
- property assignment;
- property removal;
- property enumeration;
- array indexing;
- array element assignment;
- array length;
- logical identity;
- conversion between runtime values and MuLang values.

Adapters do not define or customize truthiness. Determining truthiness MUST NOT enumerate properties or elements, access adapter members, or perform deep traversal.

The condition-semantics profile option and truthiness rules are defined in [Section 18.9](18-language-profiles.md#189-conditions).

Static mutability is intentionally not represented in the first-version type system.

An adapter MAY reject a mutation or removal at runtime. Previous completed side effects are not rolled back.

The first language version does not provide source-level operations for inspecting whether a specific property or array element is writable or whether a property is removable. `has` reports only property presence and does not imply either capability.
