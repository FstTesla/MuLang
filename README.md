# MuLang

![MuLang logo](MuLang.png)

MuLang is a small, embeddable, statically checked language for expressions and imperative programs. Providers define the available global variables, functions, and structured types. The compiler produces portable IR, which a runtime-specific exporter turns into executable code.

## Status

MuLang currently targets .NET 10 and is under active development. Version suffixes communicate release maturity:

| Suffix | Maturity |
|---|---|
| `alpha.N` | Experimental |
| `beta.N` | Prerelease |
| `rc.N` | Preview |

`N` is a positive integer. Language behavior and public APIs may change before version 1.0.

## Getting started

Install the compiler and .NET exporter:

```powershell
dotnet add package MuLang.Compiler --prerelease
dotnet add package MuLang.Exporters.DotNet --prerelease
```

| Package | Purpose |
|---|---|
| `MuLang.Core` | Runtime-neutral types, environments, profiles, and diagnostics |
| `MuLang.IR` | Public portable IR and validation |
| `MuLang.Compiler` | Source-to-IR compilation |
| `MuLang.Exporters.DotNet` | IR-to-.NET export and runtime adapters |
| `MuLang.StandardLibrary` | Optional runtime-neutral standard-library declarations and composition |
| `MuLang.StandardLibrary.DotNet` | Optional .NET standard-library implementations and runtime composition |

The standard library is opt-in and modular. Hosts select declarations from
`StandardLibraryCatalog`, compose matching .NET bindings from
`DotNetStandardLibraryModules`, and remain responsible for passing both
compositions to the compiler and runtime. The compiler, exporter, and facade
packages never import standard-library symbols implicitly.

A MuLang host:

1. Defines the global variables, functions, and structured types available to source code through an environment schema.
2. Compiles an expression or program to portable IR through `MuLangCompiler`.
3. Checks compilation diagnostics and exports the IR through `DotNetExporter`.
4. Supplies runtime values and provider functions through `DotNetRuntimeContext`.

See the [examples](https://github.com/FstTesla/MuLang/blob/main/docs/public/examples.md) for a complete compilation and execution flow.

## Documentation

- [Documentation site](https://fsttesla.github.io/MuLang/)
- [Language specification](https://github.com/FstTesla/MuLang/blob/main/docs/public/language/index.md)
- [Portable intermediate representation](https://github.com/FstTesla/MuLang/blob/main/docs/public/ir/portable-intermediate-representation.md)
- [MuIR serialization format](https://github.com/FstTesla/MuLang/blob/main/docs/public/ir/muir-format.md)
- [Examples](https://github.com/FstTesla/MuLang/blob/main/docs/public/examples.md)
- [Packages and architecture](https://fsttesla.github.io/MuLang/packages.html)
- [Standard library](https://fsttesla.github.io/MuLang/standard-library.html)
- [Stable changelog](https://github.com/FstTesla/MuLang/blob/main/CHANGELOG.md)
- [Detailed changelog](https://github.com/FstTesla/MuLang/blob/main/CHANGELOG.detailed.md)

## Feedback

Report defects and propose changes through [GitHub Issues](https://github.com/FstTesla/MuLang/issues).

## Build

```powershell
dotnet build MuLang.slnx
```

## Test

```powershell
dotnet test MuLang.slnx
```

## License

MuLang is licensed under the [Apache License 2.0](https://github.com/FstTesla/MuLang/blob/main/LICENSE).
