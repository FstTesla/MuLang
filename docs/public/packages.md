# Packages and architecture

MuLang is distributed as a set of independently referenceable packages that
share one version and release cadence.

| Package | Responsibility | Direct dependencies |
|---|---|---|
| `MuLang.Core` | Types, environment contracts, profiles, diagnostics, and source spans | None |
| `MuLang.IR` | Portable immutable IR and validation | `MuLang.Core` |
| `MuLang.Compiler` | Parsing, binding, type checking, flow analysis, and lowering to IR | `MuLang.Core`, `MuLang.IR` |
| `MuLang.Exporters.DotNet` | .NET delegate export, runtime context, and runtime adapters | `MuLang.Core`, `MuLang.IR` |
| `MuLang` | High-level .NET compilation facade | `MuLang.Core`, `MuLang.Compiler`, `MuLang.Exporters.DotNet` |
| `MuLang.StandardLibrary` | Optional runtime-independent standard-library declarations | `MuLang.Core` |
| `MuLang.StandardLibrary.DotNet` | Optional .NET implementations of standard-library declarations | `MuLang.Core`, `MuLang.StandardLibrary`, `MuLang.Exporters.DotNet` |

Use `MuLang` for ordinary .NET hosting. Exporter authors can depend directly
on `MuLang.Compiler`, `MuLang.IR`, and `MuLang.Core` without referencing the
.NET exporter or facade.

The standard-library packages are reserved for future opt-in modules and
currently expose no modules. The compiler and facade do not add constants,
functions, or structured types implicitly.
