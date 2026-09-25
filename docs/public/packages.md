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

The standard-library packages are reserved for future opt-in modules and
currently expose no modules. The compiler does not add constants, functions,
or structured types implicitly.
