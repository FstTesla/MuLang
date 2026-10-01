# Packages and architecture

MuLang is distributed as a set of independently referenceable packages that
share one version and release cadence.

| Package | Responsibility | Direct dependencies |
|---|---|---|
| `MuLang.Core` | Types, environment contracts, profiles, diagnostics, and source spans | None |
| `MuLang.IR` | Portable immutable IR, validation, and MuIR serialization | `MuLang.Core` |
| `MuLang.Compiler` | Parsing, binding, type checking, flow analysis, and lowering to IR | `MuLang.IR` |
| `MuLang.Exporters.DotNet` | .NET delegate export, runtime context, and runtime adapters | `MuLang.IR` |
| `MuLang.StandardLibrary` | Optional runtime-independent standard-library declarations | `MuLang.Core` |
| `MuLang.StandardLibrary.DotNet` | Optional .NET implementations of standard-library declarations | `MuLang.StandardLibrary`, `MuLang.Exporters.DotNet` |

Ordinary .NET hosts reference `MuLang.Compiler` and
`MuLang.Exporters.DotNet`: the compiler produces portable IR and the exporter
turns validated IR into a .NET delegate. Alternative exporters reference
`MuLang.Compiler`, `MuLang.IR`, and the runtime-specific packages they need.

`MuLang.IR` can persist an `IrProgram` as canonical UTF-8 MuIR (`.muir`) and reconstruct it without runtime-specific metadata. Deserialization establishes only the MuIR structural contract; hosts must still validate the program against the intended environment before export.

The .NET exporter exposes independent read-only and mutable array adapter contracts. Provider arrays that do not support writes implement `IDotNetReadOnlyArrayValue`; existing mutable arrays continue to implement the unchanged `IDotNetArrayValue` contract.

`DotNetProviderFunction` is the canonical context-aware provider delegate.
Hosts construct a context using these functions through
`DotNetRuntimeContext.Create`. Its invocation context provides adapter-based
array reads, controlled lazy object-property enumeration and reads, MuLang
structural equality, cancellation observation, and application-error reporting.
The original `DotNetFunction` constructor path remains temporarily available as
an obsolete compatibility API.

Exported programs expose an `ExecutionDelegate` returning `ExecutionResult` as
the canonical execution surface. The exception-based `Delegate` is derived from
it for compatibility. Both surfaces report the same structured `RuntimeError`,
including category, catchability, source span, MuLang frames, public cause, and
application data. `ExecutionResult` also retains the underlying host exception
when one is available.

The standard-library packages provide opt-in symbols and proposed modules with
separate declarative composition and .NET runtime binding.
`MuLang.StandardLibrary` exposes runtime-neutral symbol metadata, normalized
selections, modules, and environment declarations.
`MuLang.StandardLibrary.DotNet` binds matching provider values and functions.
Hosts select symbols explicitly; the compiler, exporter, and facade packages do
not add constants, functions, or structured types implicitly.

See the [standard-library guide](standard-library.md) for symbol and module
catalogs, selection rules, stable identifiers, runtime capabilities, and
collision behavior.
