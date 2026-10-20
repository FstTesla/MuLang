# Editor integration development

This guide covers building and verifying the Visual Studio extension from the
repository. User-facing installation, editor behavior, workspace configuration,
and language-server integration are documented under
[Editor support](public/editor-support/index.md).

## Components

The integration consists of:

- `MuLang.LanguageServer`, a reusable .NET 10 language server communicating
  through standard input and output;
- `MuLang.VisualStudio`, a Visual Studio 2022 and 2026 VSIX that starts the
  server;
- a TextMate grammar for immediate lexical highlighting;
- a language configuration for brackets, automatic closing, surrounding, word
  boundaries, and indentation;
- editor-oriented APIs in `MuLang.Core` and `MuLang.Compiler`.

The language server runs outside the Visual Studio process. Visual
Studio-specific dependencies do not enter the compiler or language-server
dependency graph.

## Build

After restoring the required dependencies, build the extension project on
Windows:

```powershell
dotnet build src\MuLang.VisualStudio\MuLang.VisualStudio.csproj --no-restore
```

The resulting package is written to:

```text
src\MuLang.VisualStudio\bin\Debug\net472\MuLang.VisualStudio.vsix
```

The build publishes the language server and embeds its executable, runtime
configuration, compiler assemblies, grammar, language configuration, and
registration file in the VSIX.

Release builds convert the MuLang semantic version to the numeric version
required by the VSIX manifest. Tagged workflows retain every VSIX as a GitHub
Actions artifact; beta, release-candidate, and stable workflows also attach it
to the GitHub Release.

## Manual verification

Close running Visual Studio instances, open the generated VSIX, and complete
the installer. Verify installation and behavior against the public
[requirements and installation instructions](public/editor-support/index.md#visual-studio-installation).

Open the files under [samples/valid](samples/valid) to exercise language syntax
and compiler-backed semantic classifications without requiring a host
environment. The [warning samples](samples/warnings) and
[error samples](samples/errors) contain intentional diagnostics.

Verify native property classifications and document-highlight rendering under
light and dark themes, classified multiline Quick Info, definition navigation,
and Find All References. Exercise Folder View and Solution View, including
linked MuLang files and configuration-file changes. Compare the results with
the public [editor features](public/editor-support/editor-features.md) and
[configuration rules](public/editor-support/configuration.md).
