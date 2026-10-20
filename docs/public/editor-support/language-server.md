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
| `--visual-studio` | Selects Visual Studio-specific semantic-token presentation. Other clients should omit it. |
| `--workspace-root=` | Supplies an absolute file URI as the fallback workspace root. Standard LSP workspace information takes precedence. |

Building and packaging the server is covered by the
[editor development guide](https://github.com/FstTesla/MuLang/blob/main/docs/editor-development.md).

## Supported features

The server provides diagnostics, semantic highlighting, hover, document
highlights, definitions, and references. See [editor features](editor-features.md)
for their behavior and limitations.

Clients must use full document synchronization and UTF-16 positions.
Semantic tokens are available through `textDocument/semanticTokens/full`;
incremental token responses are not supported.

A client can pair semantic tokens with the shared
[TextMate grammar](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/Grammars/MuLang.tmLanguage.json)
and [language configuration](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/MuLang-language-configuration.json)
for immediate lexical fallback and editing behavior.

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
