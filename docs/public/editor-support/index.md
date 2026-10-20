# Editor support

MuLang provides editor support for `.mu` and `.mulang` source files through a
compiler-backed language server. The Visual Studio extension combines that
server with immediate lexical highlighting, native Quick Info, and
solution-aware navigation.

## Release availability

This section describes the current source tree for the `0.4.0` tooling release
cycle, not only the latest stable package.

| Availability | Features |
| --- | --- |
| Stable, since `0.2.0` | Visual Studio extension, lexical and semantic highlighting, and live compiler diagnostics. |
| Prerelease, since `0.4.0-beta.1` | Workspace configuration, hierarchical configuration files, automatic configuration reloads, and MuLang file icons. |
| Prerelease, since `0.4.0-beta.2` | Semantic hover, document highlights, definition navigation, workspace references, and dedicated property classification. |
| Current source tree | Native classified Visual Studio Quick Info, solution-scoped reference discovery, and contextual object-literal property navigation. |

Current-source refinements may require a build newer than the listed
prereleases. Consult the
[stable changelog](https://github.com/FstTesla/MuLang/blob/main/CHANGELOG.md)
and [detailed prerelease changelog](https://github.com/FstTesla/MuLang/blob/main/CHANGELOG.detailed.md)
for the features available in a particular release.

## Visual Studio installation

The extension supports Visual Studio 2022 and Visual Studio 2026. It requires:

- Visual Studio version 17.x or 18.x;
- the Visual Studio core editor;
- a .NET 10 runtime for the bundled framework-dependent language server.

Download the VSIX attached to the desired
[GitHub Release](https://github.com/FstTesla/MuLang/releases). Choose a `0.4.0`
prerelease for the prerelease features listed above.

Close running Visual Studio instances, open the VSIX, and complete the
installer. Opening a `.mu` or `.mulang` file then activates the MuLang content
type and language client. The extension starts its bundled language server;
no separate server installation is required.

## Using the editor

- Read about [editor features](editor-features.md), including highlighting,
  hover, diagnostics, and navigation.
- Configure compilation mode, language profiles, and host symbols through
  [workspace configuration](configuration.md).
- Integrate another client through the [language server](language-server.md).

The [valid language samples](https://github.com/FstTesla/MuLang/tree/main/docs/samples/valid)
exercise syntax and compiler-backed classifications without requiring a host
environment. The
[warning samples](https://github.com/FstTesla/MuLang/tree/main/docs/samples/warnings)
and [error samples](https://github.com/FstTesla/MuLang/tree/main/docs/samples/errors)
contain intentional diagnostics.

## Supported clients and limitations

Visual Studio is the provided editor integration. Other LSP clients can use the
server, but a dedicated VS Code extension is not yet provided.

The server does not yet provide completion, rename, formatting, incremental
parsing, or incremental semantic-token responses. It analyzes MuLang source,
not serialized MuIR documents.

Build and VSIX verification instructions are maintained separately in the
[editor development guide](https://github.com/FstTesla/MuLang/blob/main/docs/editor-development.md).
