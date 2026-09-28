# MuLang Visual Studio support

`MuLang.VisualStudio` provides syntax highlighting and live compiler diagnostics for `.mu` and `.mulang` files in Visual Studio 2022.

## Components

The integration consists of:

- `MuLang.LanguageServer`, a reusable .NET 10 language server communicating through standard input and output;
- `MuLang.VisualStudio`, a Visual Studio 2022 VSIX that starts the server;
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

## Installation

Close running Visual Studio instances, open the generated VSIX, and complete the installer. Files with the `.mu` or `.mulang` extension then activate the MuLang content type and language client.

The extension requires:

- Visual Studio 2022 version 17.0 or later;
- the Visual Studio core editor;
- a .NET 10 runtime for the framework-dependent language-server executable.

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

TextMate provides immediate lexical highlighting for keywords, literals, strings, operators, and punctuation. It remains active while the language server starts and acts as a fallback if the server is unavailable.

The compiler supplements TextMate with binding-based semantic classifications for:

- named types;
- functions;
- parameters;
- local and global variables;
- object properties;
- declarations;
- read-only host symbols;
- host-provided symbols.

The language server publishes these classifications through `textDocument/semanticTokens/full` using standard LSP token types and modifiers. Semantic token positions and lengths are encoded in UTF-16 as required by the protocol.

## Current scope

The server supports initialization, full document synchronization, diagnostics, full-document semantic tokens, shutdown, and exit.

It does not yet provide completion, hover, navigation, references, rename, formatting, workspace configuration, incremental parsing, or incremental semantic-token responses.
