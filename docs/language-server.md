# MuLang Visual Studio support

`MuLang.VisualStudio` provides syntax highlighting and live compiler diagnostics for `.mu` and `.mulang` files in Visual Studio 2022 and Visual Studio 2026.

## Components

The integration consists of:

- `MuLang.LanguageServer`, a reusable .NET 10 language server communicating through standard input and output;
- `MuLang.VisualStudio`, a Visual Studio 2022 and 2026 VSIX that starts the server;
- a TextMate grammar for immediate lexical highlighting;
- a language configuration for brackets, automatic closing, surrounding, word boundaries, and indentation;
- editor-oriented APIs in `MuLang.Core` and `MuLang.Compiler`.

The language server runs outside the Visual Studio process. Visual Studio-specific dependencies therefore do not enter the compiler or language-server dependency graph.

## Build

Build the extension project:

```powershell
dotnet build src\MuLang.VisualStudio\MuLang.VisualStudio.csproj
```

The resulting package is written to:

```text
src\MuLang.VisualStudio\bin\Debug\net472\MuLang.VisualStudio.vsix
```

The build publishes the language server and embeds its executable, runtime configuration, compiler assemblies, grammar, language configuration, and registration file in the VSIX.

Release builds convert the MuLang semantic version to the numeric version required by the VSIX manifest. Tagged workflows retain every VSIX as a GitHub Actions artifact; beta, release-candidate, and stable workflows also attach it to the GitHub Release.

## Installation

Close running Visual Studio instances, open the generated VSIX, and complete the installer. Files with the `.mu` or `.mulang` extension then activate the MuLang content type and language client.

The extension requires:

- Visual Studio version 17.x or 18.x;
- the Visual Studio core editor;
- a .NET 10 runtime for the framework-dependent language-server executable.

## Highlighting smoke test

Open the files under [`samples/valid`](samples/valid) in Visual Studio to exercise the language syntax and compiler-backed semantic classifications without requiring a host environment. The [`samples/warnings`](samples/warnings) and [`samples/errors`](samples/errors) directories contain intentional diagnostics for editor validation.

## Workspace configuration

Every standalone `.mu` or `.mulang` file is analyzed as:

- a MuLang program;
- the standard profile for the latest supported language version;
- an empty host environment;
- no expected result type.

The server uses full-document synchronization. Opening and changing a document publishes versioned diagnostics; closing it clears diagnostics.

These defaults intentionally avoid guessing host-specific globals, functions,
types, expression mode, or expected result types.

Workspace-aware LSP clients can provide a `mulang` configuration independently
for each workspace folder. The server requests the section through
`workspace/configuration` when the client advertises that capability. Clients
without configuration-pull support can send the same section through
`workspace/didChangeConfiguration`.

For Visual Studio, add a file named `mulang.config.json` to the workspace root
or any of its subdirectories. For each document, the server searches its
ancestor directories up to the workspace root and composes every configuration
file from root to leaf. Each `configuration` property, when present, completely
replaces the inherited fallback configuration. The `overrides` arrays are
concatenated in root-to-leaf order.

In Folder View, Visual Studio supplies the workspace root through standard LSP
initialization. In Solution View, the VSIX reads the active solution directory
and passes it to the server as a fallback root. A standard LSP workspace root,
when available, takes precedence over this fallback.

The formal JSON Schema is
[`docs/schemas/mulang.config.schema.json`](schemas/mulang.config.schema.json).
Configuration files can associate it with the document by setting `$schema`
to a relative or absolute URI reference.

Configuration files accept `//` line comments and `/* ... */` block comments.
They also accept trailing commas in objects and arrays. Comments and trailing
commas are handled before structural and semantic validation. JSON Schema
describes the resulting data model and does not itself represent either syntax
extension.

### Configuration-file schema

The root MUST be a JSON object. It accepts only the following properties:

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `$schema` | string | No | URI reference to the JSON Schema. The server ignores it after structural validation. |
| `configuration` | configuration object | No | Complete fallback configuration selected at this hierarchy level. Omission preserves the inherited configuration; if the complete chain omits it, standalone defaults apply. |
| `overrides` | array of override objects | No | Ordered path-specific configurations appended after overrides inherited from ancestor files. Omission is equivalent to an empty array. |

Compilation properties such as `compilationMode` are not valid directly in the
root object.

Each override accepts only:

| Property | Type | Required | Description |
| --- | --- | --- | --- |
| `includes` | non-empty array of strings | Yes | Relative globs. At least one MUST match the document path. |
| `excludes` | array of strings | No | Relative globs. None may match the document path. |
| `configuration` | configuration object | Yes | Complete configuration selected by the override. |

Glob paths are relative to the directory containing `mulang.config.json` and
use `/` as the normalized separator. They support `*` within one path segment,
`?` for one non-separator character, and `**` across directory levels.
Absolute paths and `..` segments are invalid. Overrides are evaluated in array
order after concatenating ancestor files from root to leaf; the last matching
override wins. Override configurations replace rather than merge with the
fallback configuration or earlier overrides.

### Compilation configuration

A configuration object accepts only:

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `compilationMode` | `program` or `expression` | `program` | Selects program or single-expression compilation. |
| `expectedResultType` | type reference or `null` | `null` | Requires the compiled result to have the specified type. |
| `profile` | profile string or object | `latest` | Selects a standard profile and optional feature overrides. |
| `environment` | environment object or `null` | empty environment | Declares host-provided types, globals, and provider functions. |

Enum values use the canonical casing shown in this document and in the JSON
Schema. The server parses feature and compilation-mode enum values
case-insensitively; profile-version strings remain exact.

The `profile` property is either `latest`, `1`, `1.1`, or `1.2`, or an object
with the following optional properties:

| Property | Values |
| --- | --- |
| `base` | `latest`, `1`, `1.1`, `1.2` |
| `languageVersion` | `1`, `1.1`, `1.2` |
| `userDefinedFunctions` | `disabled`, `enabled` |
| `userDefinedTypes` | `disabled`, `enabled` |
| `recursion` | `disabled`, `enabled` |
| `loops` | array containing `none`, `while`, or `for` |
| `providerFunctionCalls` | `disabled`, `enabled` |
| `openObjects` | `disabled`, `propertyExistenceOnly`, `enabled` |
| `mutations` | array containing `none`, `objectProperties`, `arrayElements`, or `propertyRemoval` |
| `multiLevelLoopControl` | `disabled`, `enabled` |
| `trailingCommas` | `disabled`, `enabled` |
| `constantFolding` | `disabled`, `enabled` |
| `exceptionHandling` | `disabled`, `enabled` |
| `objectLiteralSyntax` | `legacy`, `full` |
| `conditionSemantics` | `strictBoolean`, `truthiness` |
| `shadowing` | array containing `none`, `nestedScopes`, or `globals` |

Omitted profile properties inherit from `base`, whose default is `latest`.
Array-valued feature properties are flag sets; an empty array disables every
flag in that set.

### Host environment

An environment object accepts only `types`, `globals`, and `functions`. Each
property is optional and defaults to an empty array.

A named type declaration requires string `id` and `name` properties. It can
also contain:

- `isOpen`, a Boolean that defaults to `false`;
- `properties`, an array that defaults to empty.

Each property declaration requires string `name` and a `type` reference.
`isOptional` and `isReadOnly` are optional Booleans that default to `false`.

A global declaration requires string `id`, string `name`, and a `type`
reference. A provider-function declaration requires string `id`, string
`name`, and a `returnType` reference. Its optional `parameters` array defaults
to empty. Each parameter requires string `name` and a `type` reference.

The `id` identifies the host symbol, while `name` is the identifier visible to
MuLang source.

### Type references

A type reference is one of:

- an intrinsic name: `bool`, `int`, `float`, `number`, `string`, `primitive`,
  `unknown`, `object`, `void`, or `error`;
- the `name` of a type declared in the same environment;
- a nullable reference object with `kind` set to `nullable` and a required
  `underlyingType`;
- an array reference object with `kind` set to `array`, a required
  `elementType`, and an optional Boolean `readOnly` that defaults to `false`.

Type references are recursive, so nullable arrays, arrays of nullable values,
and mutually recursive named host types are supported.

The JSON Schema validates document shape, required properties, canonical enum
values, and primitive JSON types. The language server additionally validates
named-type references, duplicate or conflicting host symbols, type placement,
profile compatibility, and other compiler invariants. Configuration updates
are applied atomically only after both structural and semantic validation
succeed.

For example:

```json
{
  "$schema": "docs/schemas/mulang.config.schema.json",
  "configuration": {
    "compilationMode": "program",
    "profile": "latest"
  },
  "overrides": [
    {
      "includes": [
        "expressions/**/*.mu"
      ],
      "excludes": [
        "expressions/generated/**"
      ],
      "configuration": {
        "compilationMode": "expression",
        "expectedResultType": "int",
        "profile": "1.2"
      }
    },
    {
      "includes": [
        "expressions/conditions/**/*.mu"
      ],
      "configuration": {
        "compilationMode": "expression",
        "expectedResultType": "bool",
        "profile": "1.2"
      }
    }
  ]
}
```

This uses program mode by default, expects integer expressions under
`expressions` except for generated files, and expects boolean expressions
under `expressions/conditions` because the last matching override wins.
Overrides are complete configurations rather than incremental merges.
When configuration files are nested, each file retains its own directory as
the base for its glob paths.

In Folder View, the VSIX recursively watches `**/mulang.config.json` and
forwards changes to the language server. In Solution View, where Visual Studio
does not forward these notifications, the language server watches the solution
directory directly. In both views, modifying, creating, deleting, or renaming a
configuration file re-evaluates the configuration hierarchy and republishes
diagnostics for open documents.

Invalid updates report an error and leave the last valid configuration active.
Documents with no configuration file in their ancestor chain use the
standalone defaults. The file name is fixed and cannot be overridden, while
its location determines the subtree to which it applies.

## Diagnostics

Compiler diagnostics retain their:

- MuLang diagnostic code;
- severity;
- message;
- source span.

MuLang stores source spans in Unicode scalar offsets. The language server converts them to zero-based UTF-16 line and character positions before publishing them through LSP, which keeps Visual Studio squiggles aligned after supplementary Unicode characters.

## Hybrid highlighting

TextMate provides immediate lexical highlighting for keywords, including the
language-version-1.2 `type` declaration keyword and `primitive` intrinsic type,
literals, strings, operators,
and punctuation. Object-property `$`, `?`, `:`, and `=` tokens retain those
ordinary classifications. It remains active while the language server starts and acts
as a fallback if the server is unavailable.

The compiler supplements TextMate with binding-based semantic classifications for:

- named types;
- source type declarations and references;
- functions, exposed as the standard LSP `method` token so Visual Studio uses its method classification;
- parameters;
- local and global variables;
- object properties;
- explicit object-property types;
- inline and nested object-type property declarations and references;
- declarations;
- read-only host symbols;
- host-provided symbols.

Named non-intrinsic types use the standard LSP `type` token and therefore Visual Studio's type classification. Intrinsic type keywords remain lexical TextMate classifications.

The language server publishes these classifications through `textDocument/semanticTokens/full`. Semantic token positions and lengths are encoded in UTF-16 as required by the protocol.

By default, the server uses standard LSP token types and modifiers. The Visual Studio client starts it in a dedicated presentation mode: functions and named structured types then use Visual Studio's native `method name` and `class name` classifications, while unsupported token modifiers are omitted so they cannot override semantic colors with the plain-text classification.

Compiler lowering also preserves these source names as optional well-known
portable-IR diagnostic attributes. The attributes do not change semantic
tokens or source-language behavior, but downstream runtime diagnostics can
display function, local, parameter, provider-symbol, and structural type
aliases after compilation or MuIR round trips.

## Current scope

The server supports initialization, workspace folders and configuration, full
document synchronization, diagnostics, full-document semantic tokens,
shutdown, and exit.

It does not yet provide completion, hover, navigation, references, rename,
formatting, incremental parsing, or incremental semantic-token responses.
