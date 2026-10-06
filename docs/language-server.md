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

Open [`samples/highlighting.mu`](samples/highlighting.mu) in Visual Studio to exercise the TextMate and compiler-backed semantic classifications without requiring a host environment.

## Initial document model

Every standalone `.mu` or `.mulang` file is analyzed as:

- a MuLang program;
- the standard profile for the latest supported language version;
- an empty host environment;
- no expected result type.

The server uses full-document synchronization. Opening and changing a document publishes versioned diagnostics; closing it clears diagnostics.

These defaults intentionally avoid guessing host-specific globals, functions, types, expression mode, or expected result types. Workspace configuration for those inputs is planned separately.

## Diagnostics

Compiler diagnostics retain their:

- MuLang diagnostic code;
- severity;
- message;
- source span.

MuLang stores source spans in Unicode scalar offsets. The language server converts them to zero-based UTF-16 line and character positions before publishing them through LSP, which keeps Visual Studio squiggles aligned after supplementary Unicode characters.

## Hybrid highlighting

TextMate provides immediate lexical highlighting for keywords, including the
language-version-1.2 `primitive` intrinsic type, literals, strings, operators,
and punctuation. Object-property `$`, `?`, `:`, and `=` tokens retain those
ordinary classifications. It remains active while the language server starts and acts
as a fallback if the server is unavailable.

The compiler supplements TextMate with binding-based semantic classifications for:

- named types;
- functions, exposed as the standard LSP `method` token so Visual Studio uses its method classification;
- parameters;
- local and global variables;
- object properties;
- explicit object-property types;
- declarations;
- read-only host symbols;
- host-provided symbols.

Named non-intrinsic types use the standard LSP `type` token and therefore Visual Studio's type classification. Intrinsic type keywords remain lexical TextMate classifications.

The language server publishes these classifications through `textDocument/semanticTokens/full`. Semantic token positions and lengths are encoded in UTF-16 as required by the protocol.

By default, the server uses standard LSP token types and modifiers. The Visual Studio client starts it in a dedicated presentation mode: functions and named structured types then use Visual Studio's native `method name` and `class name` classifications, while unsupported token modifiers are omitted so they cannot override semantic colors with the plain-text classification.

## Current scope

The server supports initialization, full document synchronization, diagnostics, full-document semantic tokens, shutdown, and exit.

It does not yet provide completion, hover, navigation, references, rename, formatting, workspace configuration, incremental parsing, or incremental semantic-token responses.
