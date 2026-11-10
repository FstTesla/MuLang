# Language-server integration

This page is for users integrating MuLang with an LSP-compatible editor.
Visual Studio users do not need to configure or launch the server separately.

`MuLang.LanguageServer` requires .NET 10 and communicates through standard input
and output. See [release availability](index.md#release-availability) for the
versions covered by this documentation.

## Starting the server

Configure the client to launch the `MuLang.LanguageServer` executable from a
build or publish output and connect its standard-input and standard-output
streams using standard LSP message framing. The framework-dependent server
requires a .NET 10 runtime.

The executable accepts two optional arguments:

| Argument | Purpose |
| --- | --- |
| `--visual-studio` | Selects Visual Studio-specific presentation and capability activation. Other clients should omit it. |
| `--workspace-root=` | Supplies an absolute file URI as the fallback workspace root. Standard LSP workspace information takes precedence. |

Building and packaging the server is covered by the
[editor development guide](https://github.com/FstTesla/MuLang/blob/main/docs/editor-development.md).

## Supported features

The server provides diagnostics, semantic highlighting, hover, document
highlights, definitions, references, document symbols, and structural
folding. See [editor features](editor-features.md)
for their behavior and limitations.

Clients must use full document synchronization and UTF-16 positions.
Semantic tokens are available through `textDocument/semanticTokens/full`;
incremental token responses are not supported.

`textDocument/documentSymbol` and `textDocument/foldingRange` serve both MuLang
and MuIR. Advertise `hierarchicalDocumentSymbolSupport` for nested declarations
and `lineFoldingOnly` when the client cannot consume character-based folds.
MuLang retains recoverable syntax-based results after source errors; MuIR
requires successful complete reading.

In Visual Studio mode, clients advertising
`textDocument.documentSymbol.dynamicRegistration` receive the document-symbol
registration after the first MuLang or MuIR `textDocument/didOpen`, rather than
through the initial static capability. Registration happens once per server
session, after storing the open document, and does not wait for successful
semantic analysis. Opening a configuration file alone does not trigger it.
This defers navigation-bar activation until a source document is associated
with the client. Other clients and Visual Studio clients without dynamic
registration retain the initial static capability. Registration errors are
reported through `window/showMessage`.

Clients can also synchronize `mulang.config.json` documents to receive live
syntax errors and configuration warnings through `textDocument/publishDiagnostics`.
Configuration documents use the same full synchronization and UTF-16 positions,
but are never analyzed as MuLang source. Send `textDocument/didSave` after saving
and `textDocument/didClose` when closing a configuration document.

A client can pair semantic tokens with the shared
[TextMate grammar](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/Grammars/MuLang.tmLanguage.json)
and [language configuration](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/MuLang-language-configuration.json)
for immediate lexical fallback and editing behavior.

## MuIR documents

Use language ID `muir` for MuIR 1 and 2 documents. `.muir` URI recognition is
also supported, including the existing Visual Studio integration. Use the same
server process, full synchronization, and UTF-16 positions.

MuIR reuses hover, `mulang/quickInfo`, definitions, references, highlights, and
full semantic tokens. It also supports `textDocument/documentSymbol` and
`textDocument/foldingRange`. Advertise `hierarchicalDocumentSymbolSupport` for nested symbols;
otherwise the server returns flat symbol information with container names.
Fold ranges include their closing delimiters. `lineFoldingOnly` omits character
positions and includes the closing line. The existing semantic-token indices
remain unchanged; the appended
`label` classification covers blocks and regions (`label name` in Visual Studio).

Pair this with the
[MuIR TextMate grammar](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/Grammars/MuIR.tmLanguage.json)
and [MuIR language configuration](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/MuIR-language-configuration.json).

No MuIR diagnostics are published, including on document close or configuration
refresh. Invalid documents expose no partial or stale semantic result. Navigation
and references are document-local, and consultation does not consult MuLang
host configuration or validate execution eligibility.

## Workspace configuration

Workspace-aware LSP clients can provide a `mulang` configuration independently
for each workspace folder. The server requests the section through
`workspace/configuration` when the client advertises that capability. Clients
without configuration-pull support can send the same section through
`workspace/didChangeConfiguration`.

The section contains the compilation configuration described under
[compilation configuration](configuration.md#compilation-configuration), not
the configuration-file wrapper containing `$schema`, `configuration`, and
`overrides`. File-based hierarchy, override evaluation, and validation are
documented on the [workspace configuration](configuration.md) page.
