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

> For example, this expression is valid only if the static environment declares globals named `price` and `taxRate` with compatible numeric types:
>
> ```text
> price * taxRate
> ```

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

> If the static environment declares `next(int): int`, source may call only that declared operation:
>
> ```text
> next(41)
> ```
>
> Other methods or members of the host implementation are not implicitly visible.

Provider functions are synchronous. A provider invocation MAY receive an
invocation-scoped runtime context exposing:

- cancellation observation;
- runtime object and array kind checks;
- read-only array inspection;
- object property enumeration and reads;
- object and array mutation attempts;
- construction of validated arrays, structured objects, and error values;
- MuLang structural equality;
- MuLang identity equality;
- reporting of expected application failures at the provider call span.

These services MUST use the same value adapters, structural semantics,
execution controls, and runtime-error model as ordinary MuLang execution. An
invocation context MUST NOT remain usable after its provider invocation
completes. Values created through the context MUST conform to the supplied
array or structured-object type. Array mutability is determined by its array
type, and object mutation is subject to the object's declared capabilities.
Mutation methods report rejection without converting it into an exception.
Provider operations and factory enumeration participate in cancellation and
resource-budget accounting.

Object-property name enumeration is lazy. Acquiring the enumeration and
advancing it are independently subject to execution controls, and an enumerator
cannot be advanced after the provider invocation completes.

A provider-reported application failure is distinct from an unexpected
exception thrown by the provider implementation. The former is a catchable
`Application` error; the latter is an uncatchable `Provider` error.

## 11.3. Environment compatibility

An executable result MUST be associated with the static environment against which it was compiled.

Execution MUST be rejected when declarations that can affect compilation are incompatible. Relevant declarations include names, types, function signatures, object schemas, and mutability capabilities.

Mutable and read-only array types and object-property capabilities are distinct
for compatibility purposes.
The language version and the presence of `primitive` in any recursively
referenced type also contribute to compatibility.

> For example, an executable compiled against a global `values: int[]` cannot be executed with an environment that redeclares the global as `values: int[]$`.

## 11.4. Boundary values

Host-specific values MUST cross the MuLang boundary through explicit representations of the operations required by their declared MuLang types. They MUST NOT acquire object properties, array behavior, or callable behavior through implicit reflection or unrelated host-language conventions.

Global values, function arguments entering MuLang, and host-provided function results MUST conform recursively to their declared MuLang types.

A boundary value declared as `primitive` MUST be represented as a Boolean,
signed 64-bit integer, binary64 floating-point value, or string. No wrapper or
tagged union is introduced, and the concrete representation is preserved.

An object representation defines the applicable subset of:

- property presence and lookup;
- property assignment;
- property removal;
- property enumeration;
- logical identity.

A host value declared with read-only properties MUST preserve those restrictions
through every alias exposed to MuLang. An adapter MAY reject operations more
strictly than its static declaration.

An array representation defines:

- element reads;
- optional element writes;
- length;
- logical identity.

A value of type `T[]` MUST support element writes. A value of type `T[]$` requires only element reads. A mutable array MAY also be observed through a read-only view that preserves its logical identity.

A value declared as `error` MUST implement the host error-value contract. The
.NET contract is `IDotNetErrorValue`, which extends the object and
per-property-capability adapters and exposes the underlying `RuntimeError`.
The adapter MUST expose exactly the built-in error properties with read-only
capability. Error identity is the identity of the underlying `RuntimeError`,
not the adapter instance.

Boundary validation and deep value operations are subject to the execution controls defined in [Section 13](13-execution-controls.md).

Boundary representations do not define or customize truthiness. Determining truthiness MUST NOT enumerate properties or elements, read object members, or perform deep traversal.

The condition-semantics profile option and truthiness rules are defined in [Section 14.8](14-language-profiles.md#148-conditions).

A host-provided object or array MAY reject a mutation or removal at runtime. Previous completed side effects are not rolled back.

The .NET runtime's optional `IDotNetObjectPropertyCapabilities` interface exposes
known read-only capability for checked conformance. Literal objects implement it
and enforce required, optional, mutable, and read-only declarations internally.

MuLang provides no source-level operation for testing whether a specific property or array element is writable or whether a property is removable. `has` reports only property presence and does not imply either capability.

> Given a host global `item: object`, this expression tests only whether the property is present:
>
> ```text
> item has "value"
> ```
>
> It does not guarantee that `item.value = 1;` or `item.value~;` will succeed.
