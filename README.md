# MuLang

MuLang is a small, embeddable, statically checked language for expressions and imperative programs. Providers define the available global variables, functions, and structured types, while the compiler produces an executable delegate for a compatible runtime environment.

The project currently targets .NET 10 and is under initial design and implementation.

## Documentation

The draft language specification is available in [docs/language-specification.md](docs/language-specification.md).

## Build

```powershell
dotnet build MuLang.slnx
```

## Test

```powershell
dotnet test MuLang.slnx
```
