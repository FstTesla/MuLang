# Remaining Packaging and Structure Work

## Current state

The solution is split into independently packageable Core, IR, Compiler,
.NET exporter, facade, and standard-library assemblies. Each production
project has a matching test project, only matching test assemblies receive
internal access, and the public IR supports alternative exporters.

Packaging, API governance, package verification, publication workflows, and
DocFX operate on the complete package set. All packages share one version and
release tag. Publishing to NuGet.org remains outside the current scope.

## 1. Optional standard library

A future macro-activity is an optional standard library of basic constants and functions.

The standard library MUST preserve the principle that MuLang provides no implicit symbols. A host opts in explicitly and selects the modules or individual symbols to import into its environment.

The design should cover:

- a separate assembly and package from the compiler/runtime core;
- small cohesive modules rather than one indivisible environment;
- immutable and deterministic composition into an `EnvironmentBuilder` or `EnvironmentSchema`;
- selective import of modules and, where useful, individual symbols;
- stable provider identifiers for every exported constant and function;
- matching static declarations and .NET runtime implementations;
- explicit collision detection with provider-defined symbols and other imported modules;
- compatibility with language profiles;
- deterministic environment fingerprints independent of import order;
- documentation of runtime representation, error behavior, budget cost, and nullability;
- no dependency from the core compiler package back to the standard-library package.

Candidate modules and exact APIs require a dedicated design phase. No constants, functions, structured types, or modules are selected yet.

## 2. Later language features

After the project split and its package contracts are stable, the current feature roadmap is:

1. Comments and their language-profile setting.
2. User-defined structured types and their language-profile setting.
3. First-class functions, function types, and their language-profile setting.

Each feature requires specification decisions before implementation.

## Next planning step

Design the first standard-library modules, including their declarative
contracts, stable provider identifiers, selective import API, .NET
implementations, collision behavior, and documentation.
