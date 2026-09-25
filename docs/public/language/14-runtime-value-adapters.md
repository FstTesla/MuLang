# 14. Runtime value adapters

Runtime-specific values MUST be accessed through explicit adapters rather than implicit reflection.

The .NET runtime accepts object and array values only through its explicit object and array adapter interfaces. CLR dictionaries, lists, arrays, POCOs, and other host values are not recognized implicitly.

`IDotNetReadOnlyArrayValue` supplies stable identity, count, and element reads. `IDotNetArrayValue` independently supplies the same read operations plus element writes, preserving compatibility with existing mutable adapters. A runtime value for `T[]$` may implement either interface; a runtime value for `T[]` MUST implement the mutable interface.

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

Language version 1 does not represent static array mutability. Language version 1.1 represents read-only array capability in the static type while retaining runtime adapter capability checks.

An adapter MAY reject a mutation or removal at runtime. Previous completed side effects are not rolled back.

Language version 1 does not provide source-level operations for inspecting whether a specific property or array element is writable or whether a property is removable. `has` reports only property presence and does not imply either capability.
