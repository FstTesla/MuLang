# Packages and architecture

MuLang is distributed as a set of independently referenceable packages that
share one version and release cadence.

| Package | Responsibility | Direct dependencies |
|---|---|---|
| `MuLang.Core` | Types, environment contracts, profiles, diagnostics, and source spans | None |
| `MuLang.IR` | Portable immutable IR and validation | `MuLang.Core` |
| `MuLang.Compiler` | Parsing, binding, type checking, flow analysis, and lowering to IR | `MuLang.IR` |
| `MuLang.Exporters.DotNet` | .NET delegate export, runtime context, and runtime adapters | `MuLang.IR` |
| `MuLang.StandardLibrary` | Optional runtime-independent standard-library declarations | `MuLang.Core` |
| `MuLang.StandardLibrary.DotNet` | Optional .NET implementations of standard-library declarations | `MuLang.StandardLibrary`, `MuLang.Exporters.DotNet` |

Ordinary .NET hosts reference `MuLang.Compiler` and
`MuLang.Exporters.DotNet`: the compiler produces portable IR and the exporter
turns validated IR into a .NET delegate. Alternative exporters reference
`MuLang.Compiler`, `MuLang.IR`, and the runtime-specific packages they need.

The standard-library packages are reserved for future opt-in modules and
currently expose no modules. The compiler does not add constants, functions,
or structured types implicitly.
