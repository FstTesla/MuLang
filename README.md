# MuLang

![MuLang logo](MuLang.png)

MuLang is a small, embeddable, statically checked language for expressions and imperative programs. Providers define the available global variables, functions, and structured types, while the compiler produces an executable delegate for a compatible runtime environment.

## Status

MuLang currently targets .NET 10 and is under active development. Version suffixes communicate release maturity:

| Suffix | Maturity |
|---|---|
| `alpha.N` | Experimental |
| `beta.N` | Prerelease |
| `rc.N` | Preview |

`N` is a positive integer. Language behavior and public APIs may change before version 1.0.

## Getting started

Install the latest prerelease package:

```powershell
dotnet add package MuLang --prerelease
```

The `MuLang` package is the high-level .NET facade. Lower-level packages are
available for hosts and exporter authors:

| Package | Purpose |
|---|---|
| `MuLang.Core` | Runtime-neutral types, environments, profiles, and diagnostics |
| `MuLang.IR` | Public portable IR and validation |
| `MuLang.Compiler` | Source-to-IR compilation |
| `MuLang.Exporters.DotNet` | IR-to-.NET export and runtime adapters |
| `MuLang` | High-level .NET facade |
| `MuLang.StandardLibrary` | Reserved for optional runtime-neutral standard-library declarations |
| `MuLang.StandardLibrary.DotNet` | Reserved for optional .NET standard-library implementations |

The standard-library packages intentionally contain no modules yet. The
compiler and facade never import standard-library symbols implicitly.

A MuLang host:

1. Defines the global variables, functions, and structured types available to source code through an environment schema.
2. Compiles an expression or program through `MuLangCompiler`.
3. Checks compilation diagnostics before obtaining the executable delegate.
4. Supplies runtime values and provider functions through `DotNetRuntimeContext`.

See the [examples](https://github.com/FstTesla/MuLang/blob/main/docs/public/examples.md) for a complete compilation and execution flow.

## Documentation

- [Documentation site](https://fsttesla.github.io/MuLang/)
- [Language specification](https://github.com/FstTesla/MuLang/blob/main/docs/public/language/index.md)
- [Examples](https://github.com/FstTesla/MuLang/blob/main/docs/public/examples.md)
- [Packages and architecture](https://fsttesla.github.io/MuLang/packages.html)
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
