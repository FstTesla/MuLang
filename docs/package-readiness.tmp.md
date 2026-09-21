# Remaining Packaging and Structure Work

## Current state

The initial package infrastructure is complete and validated:

- tag-derived versioning;
- strong-name signing;
- package metadata, README, icon, license, XML documentation, and symbol package;
- Source Link and deterministic CI builds;
- PublicApiAnalyzers and package validation against dynamically selected published baselines;
- local and published-package consumer verification;
- GitHub Packages publication;
- release, changelog, preflight, and post-release workflows;
- DocFX documentation published through GitHub Pages.

Publishing to NuGet.org is outside the current scope.

## 1. Split the production project

The next macro-activity is to replace the single production project with explicitly designed assemblies and packages before adding more language features.

The design phase MUST define:

- package and assembly identities;
- dependency direction and prevention of circular references;
- which contracts must remain public across assembly boundaries;
- which implementation types can become internal;
- ownership of compiler, type-system, environment, IR, and .NET runtime APIs;
- whether a facade or aggregate package preserves the current consumer experience;
- strong-name and `InternalsVisibleTo` requirements;
- test-project boundaries;
- documentation and API-reference grouping;
- migration of existing consumers from the current `MuLang` package.

The split MUST preserve:

- language behavior and diagnostics;
- the public compilation facade unless an intentional breaking change is documented;
- package verification and Source Link;
- API compatibility governance per package;
- deterministic environment and language-profile fingerprints;
- runtime-independent IR boundaries.

Packaging work required by the split:

1. Assign package metadata only to projects intended for distribution.
2. Generate one `.nupkg`, `.snupkg`, XML documentation file, and API baseline per package.
3. Update package verification to validate the complete expected package set and dependency graph.
4. Update DocFX to generate grouped API documentation from every public assembly.
5. Update baseline discovery and package validation for independently versioned packages if the packages do not share one version.
6. Define whether all packages are released from the same tag or can acquire independent release cadences.
7. Validate consumption both through individual packages and through any aggregate package.

The split should be implemented only after a separate package-boundary proposal has been reviewed.

## 2. Optional standard library

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

## 3. Later language features

After the project split and its package contracts are stable, the current feature roadmap is:

1. Comments and their language-profile setting.
2. User-defined structured types and their language-profile setting.
3. First-class functions, function types, and their language-profile setting.

Each feature requires specification decisions before implementation.

## Next planning step

Create and review a dedicated solution-split plan containing:

- the proposed project and package graph;
- the mapping of every current namespace and public type;
- visibility changes;
- migration and compatibility impact;
- an incremental implementation sequence;
- build, test, package, documentation, and release acceptance criteria.
