# 11. Host environment and runtime values

## 11.1. Static environment

The host supplies an immutable static environment containing:

- global variable declarations;
- function declarations;
- structured object declarations.

Every structured object type referenced directly or indirectly by a global, function parameter, function return type, array element, nullable type, or structured property MUST be registered in the static environment. Every reference to the same stable type identifier MUST resolve to the same declaration.

Structured object declarations MAY be self-recursive or mutually recursive. The complete declaration graph MUST be available before compilation begins, and no partially defined declaration is observable.

Profile compatibility requirements for open structured types are defined in [Section 14.4](14-language-profiles.md#144-open-objects).

Source names are resolved exclusively against declarations in the source program and the static environment.

## 11.2. Execution environment

Execution receives an environment compatible with the static environment used during compilation.

The execution environment supplies:

- global values;
- implementations of host-provided functions;
- representations of object and array values;
- cancellation state;
- execution-limit state.

The execution environment MUST expose no source-level operation that was not declared by the static environment or required by this specification.

The host is trusted. A MuLang type restricts operations available to MuLang source but does not restrict operations that the host itself may perform on a value.

## 11.3. Environment compatibility

An executable result MUST be associated with the static environment against which it was compiled.

Execution MUST be rejected when declarations that can affect compilation are incompatible. Relevant declarations include names, types, function signatures, object schemas, and mutability capabilities.

Mutable and read-only array types are distinct for compatibility purposes.

## 11.4. Boundary values

Host-specific values MUST cross the MuLang boundary through explicit representations of the operations required by their declared MuLang types. They MUST NOT acquire object properties, array behavior, or callable behavior through implicit reflection or unrelated host-language conventions.

Global values, function arguments entering MuLang, and host-provided function results MUST conform recursively to their declared MuLang types.

An object representation defines the applicable subset of:

- property presence and lookup;
- property assignment;
- property removal;
- property enumeration;
- logical identity.

An array representation defines:

- element reads;
- optional element writes;
- length;
- logical identity.

A value of type `T[]` MUST support element writes. A value of type `T[]$` requires only element reads. A mutable array MAY also be observed through a read-only view that preserves its logical identity.

Boundary validation and deep value operations are subject to the execution controls defined in [Section 13](13-execution-controls.md).

Boundary representations do not define or customize truthiness. Determining truthiness MUST NOT enumerate properties or elements, read object members, or perform deep traversal.

The condition-semantics profile option and truthiness rules are defined in [Section 14.8](14-language-profiles.md#148-conditions).

A host-provided object or array MAY reject a mutation or removal at runtime. Previous completed side effects are not rolled back.

MuLang provides no source-level operation for testing whether a specific property or array element is writable or whether a property is removable. `has` reports only property presence and does not imply either capability.
