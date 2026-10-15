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

Workspace-aware clients can provide a `mulang` configuration independently for
each workspace folder. The server requests the section through the standard
`workspace/configuration` request when the client advertises that capability.
Clients without configuration-pull support can send the same section through
`workspace/didChangeConfiguration`. Visual Studio instead loads the dedicated
workspace file described below.

For Visual Studio, add a file named `mulang.config.json` to the workspace root
or any of its subdirectories. For each document, the server searches its
ancestor directories and uses the nearest configuration file. Nested files
replace rather than merge with configurations from higher directories.

In Open Folder, Visual Studio supplies the workspace root through standard LSP
initialization. In Solution View, the VSIX reads the active solution directory
and passes it to the server as a fallback root. A standard LSP workspace root,
when available, takes precedence over this fallback.

The root object supports:

- `configuration`: the optional fallback configuration;
- `overrides`: an ordered array of path-specific configurations.

No compilation setting is valid directly in the root object. If
`configuration` is omitted, the standalone defaults provide the fallback.
Each override requires `includes` and `configuration`. `includes` is a
non-empty array of globs; `excludes` is an optional array of globs. Paths are
relative to the directory containing `mulang.config.json`, use `/` as the
normalized separator, and support `*`, `?`, and `**`. An override matches when
at least one inclusion matches and no exclusion matches. If multiple overrides
match, the last one wins.

For example:

```json
{
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

The VSIX recursively watches `**/mulang.config.json` and reloads affected
documents through `workspace/didChangeWatchedFiles`. Invalid updates report an
error and leave the last valid configuration active. Creating or deleting a
file re-evaluates the nearest configuration for open documents. Documents with
no configuration file in their ancestor chain use the standalone defaults.

No entry in `.vs\VSWorkspaceSettings.json` is required. The file name is fixed
and cannot be overridden, while its location determines the subtree to which
it applies. Keeping structured settings in dedicated files avoids a Visual
Studio LSP client limitation that prevents object-valued workspace settings
from being converted to JSON.

The section supports:

- `compilationMode`: `program` or `expression`;
- `expectedResultType`: a type reference or `null`;
- `profile`: a standard base profile and optional feature overrides;
- `environment`: declarative types, globals, and provider functions.

The profile can be a base-profile string or an object. `base` accepts `latest`,
`1`, `1.1`, or `1.2`. Object profiles can override `languageVersion`,
`userDefinedFunctions`, `userDefinedTypes`, `recursion`, `loops`,
`providerFunctionCalls`, `openObjects`, `mutations`,
`multiLevelLoopControl`, `trailingCommas`, `constantFolding`,
`exceptionHandling`, `objectLiteralSyntax`, `conditionSemantics`, and
`shadowing`. Flag settings use arrays of enum names; all other feature settings
use their enum names case-insensitively.

An environment contains optional `types`, `globals`, and `functions` arrays.
Named types require `id` and `name`, and can specify `isOpen` and `properties`.
Properties require `name` and `type`, with optional `isOptional` and
`isReadOnly` flags. Globals require `id`, `name`, and `type`. Functions require
`id`, `name`, `parameters`, and `returnType`; each parameter requires `name`
and `type`.

A type reference is either the name of a declared type, one of the intrinsic
names `bool`, `int`, `float`, `number`, `string`, `primitive`, `unknown`,
`object`, `void`, or `error`, or an object with `kind` set to `nullable` or
`array`. Nullable references use `underlyingType`; array references use
`elementType` and can set `readOnly`. Declared types are resolved as a graph,
so mutually recursive host types are supported. The compiler still enforces
where value types and `void` are valid.

Configuration updates are validated atomically per workspace folder. A valid
update reanalyzes open documents with the same resolved settings used for
diagnostics and semantic tokens. An invalid update produces an LSP error
message and leaves the last valid configuration active.

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
