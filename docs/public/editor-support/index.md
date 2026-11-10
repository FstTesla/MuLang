# Editor support

MuLang provides editor support for `.mu` and `.mulang` source files through a
Visual Studio extension, with syntax highlighting, hover information,
diagnostics, navigation, document symbols, and instruction-block folding.
The same extension supports consulting `.muir`
documents in MuIR formats 1 and 2, without configuring a host environment.

## Release availability

These pages describe the `0.4.0` tooling release under development. Some
features require a prerelease or a build from the current source tree.
Consult the
[stable changelog](https://github.com/FstTesla/MuLang/blob/main/CHANGELOG.md)
and [detailed prerelease changelog](https://github.com/FstTesla/MuLang/blob/main/CHANGELOG.detailed.md)
for the features available in a particular release.

## Visual Studio installation

The extension requires:

- Visual Studio 2022 or Visual Studio 2026;
- a .NET 10 runtime.

Download the VSIX attached to the desired
[GitHub Release](https://github.com/FstTesla/MuLang/releases).

Close running Visual Studio instances, open the VSIX, and complete the
installer. Opening a `.mu`, `.mulang`, or `.muir` file activates the extension.
No separate language-server installation is required.

## Using the editor

- Read about [editor features](editor-features.md), including highlighting,
  hover, diagnostics, and navigation.
- Configure compilation mode, language profiles, and host symbols through
  [workspace configuration](configuration.md).
- Integrate another client through the [language server](language-server.md).

The [valid language samples](https://github.com/FstTesla/MuLang/tree/main/docs/samples/valid)
can be opened without configuring a host environment.

## Supported clients and limitations

Visual Studio is the provided editor integration. Other LSP clients can use the
server, but a dedicated VS Code extension is not yet provided.

Completion, rename, and formatting are not yet available.

MuIR files remain normally editable. Their semantic features require complete
successful reading; invalid or unsupported documents retain lexical coloring
only. MuIR diagnostics, execution validation, source-MuLang navigation, and
automatic generation or regeneration are not provided.
