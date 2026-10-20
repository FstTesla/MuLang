# Language-server integration

`MuLang.LanguageServer` is a reusable .NET 10 language server communicating
through standard input and output. It runs outside the Visual Studio process;
Visual Studio-specific dependencies do not enter the compiler or server
dependency graph.

See [release availability](index.md#release-availability) for the features
available in stable packages, prereleases, and the current source tree.
The provided client is the Visual Studio extension; another LSP client can
integrate the same server without duplicating compiler functionality.

## Starting the server

Configure the client to launch the `MuLang.LanguageServer` executable from a
build or publish output and connect its standard-input and standard-output
streams using standard LSP message framing. The framework-dependent server
requires a .NET 10 runtime. Diagnostic process failures are written to standard
error, not the protocol stream.

The executable accepts two optional arguments:

| Argument | Purpose |
| --- | --- |
| `--visual-studio` | Selects Visual Studio-specific semantic-token presentation. Other clients should omit it. |
| `--workspace-root=` | Supplies an absolute file URI as the fallback workspace root. Standard LSP workspace information takes precedence. |

The Visual Studio extension launches its bundled server and supplies these
arguments when applicable. Building and packaging the server is covered by the
[editor development guide](https://github.com/FstTesla/MuLang/blob/main/docs/editor-development.md).

## Protocol support

The server supports initialization, workspace folders and configuration, full
document synchronization, diagnostics, full-document semantic tokens, hover,
document highlights, definitions, references, shutdown, and exit.

| Capability or method | Behavior |
| --- | --- |
| Full document synchronization | Opening and changing a document publishes versioned diagnostics; closing it clears diagnostics. |
| `textDocument/semanticTokens/full` | Returns compiler-bound full-document semantic classifications. |
| `hoverProvider` / `textDocument/hover` | Returns categorized signatures for supported hover targets. |
| `documentHighlightProvider` / `textDocument/documentHighlight` | Returns matching symbol occurrences or associated control-flow keywords in the current document. |
| `definitionProvider` / `textDocument/definition` | Returns a source definition when one exists. |
| `referencesProvider` / `textDocument/references` | Finds references in the scope determined by the selected symbol's identity and honors `context.includeDeclaration`. |

The server does not provide completion, rename, formatting, incremental
parsing, or incremental semantic-token responses. The
[editor features](editor-features.md) page defines supported symbols,
navigation scopes, dynamic-property behavior, and other user-visible details.

## Positions and source metadata

MuLang compiler spans use Unicode scalar offsets. All LSP request positions,
response ranges, and semantic-token positions and lengths use UTF-16.

Compiler lowering also preserves source names as optional well-known
portable-IR diagnostic attributes. The attributes do not change semantic
tokens or source-language behavior, but downstream runtime diagnostics can
display function, local, parameter, provider-symbol, and structural type
aliases after compilation or MuIR round trips.

## Semantic-token presentation

Standard LSP clients receive the `type`, `method`, `parameter`, `variable`, and
`property` token types. Named non-intrinsic types use `type`; functions use
`method`. Intrinsic type keywords remain lexical TextMate classifications.

With `--visual-studio`, named structured types, functions, and properties use
the native `class name`, `method name`, and `property name` classifications.
Unsupported token modifiers are omitted so they cannot override semantic
colors with the plain-text classification.

A client can pair semantic tokens with the shared
[TextMate grammar](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/Grammars/MuLang.tmLanguage.json)
and [language configuration](https://github.com/FstTesla/MuLang/blob/main/src/MuLang.VisualStudio/MuLang-language-configuration.json)
for immediate lexical fallback and editing behavior.

## Hover presentation

Clients advertising Markdown receive italic `(category)` prefixes and fenced
MuLang code blocks for multiline signatures. Other clients receive
`[category]` prefixes with the same signature layout. Unsupported targets and
unopened or closed documents return `null`.

The compiler's hover item collection supports multiple categorized entries.
Items expose classified signature parts alongside their plain-text content.
The Visual Studio extension consumes them through the custom
`mulang/quickInfo` request for native classified Quick Info. Other clients
should use standard `textDocument/hover`.

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

For reference discovery, open documents take precedence over disk content, and
each searched document uses its own effective workspace configuration.
Standard clients receive LSP locations. The Visual Studio integration adds
reference-kind presentation and supplies the current solution's MuLang file
list when searching in Solution View.
