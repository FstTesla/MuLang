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
- MuLang and MuIR TextMate grammars for immediate lexical highlighting;
- language configurations for brackets, automatic closing, surrounding, word
  boundaries, and indentation;
- editor-oriented APIs in `MuLang.Core`, `MuLang.Compiler`, and `MuLang.IR`.

The language server runs outside the Visual Studio process. Visual
Studio-specific dependencies do not enter the compiler or language-server
dependency graph.

The dedicated `MuIR` content type associates `.muir` with the same remote-code
base as MuLang. The existing language client and classified Quick Info provider
serve both languages; no additional server process or VS-specific outline
bridge is introduced. MuIR lexical assets cover versions 1 and 2 independently
of successful semantic analysis. The external LSP language ID is `muir`; Visual
Studio also activates support by extension.

`Grammars\MuIR.tmLanguage.json` and `MuIR-language-configuration.json` are
registered through `MuLang.pkgdef` and packaged by the VSIX project. The existing
MuLang file icon is reused. Keep lexical rules aligned with the
[MuIR wire format](public/ir/muir-format.md), especially `#` comments,
brace-delimited Unicode escapes, and reference-like text inside strings.

MuIR references pass through the existing middleware unchanged: they must not
enumerate MuLang solution sources or receive `_muLang_solutionDocumentUris`.
MuLang references retain solution membership filtering. Semantic edit
invalidation remains the responsibility of full-document LSP synchronization
and the shared language server.

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
configuration, compiler assemblies, both grammars, language configurations, and
registration file in the VSIX.

Run `eng\Verify-Vsix.ps1 -Path <vsix-path> -Version <manifest-version>` to check
the package identity and required assets, including both MuIR JSON resources.
Inspect the packaged `MuLang.pkgdef` for `source.muir`, `MuIR`, and the `.muir`
icon registration. A successful package check does not verify editor UI behavior.

Release builds convert the MuLang semantic version to the numeric version
required by the VSIX manifest. Tagged workflows retain every VSIX as a GitHub
Actions artifact; beta, release-candidate, and stable workflows also attach it
to the GitHub Release.

## Manual verification

Save work and close Visual Studio instances yourself, open
`src\MuLang.VisualStudio\bin\Debug\net472\MuLang.VisualStudio.vsix`, select the
intended Visual Studio 2022 or 2026 installation, and complete the installer.
Restart that instance and confirm MuLang is installed in **Extensions > Manage
Extensions**. Do not automate installer dialogs or terminate Visual Studio
processes. Verify installation and behavior against the public
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

Verify live syntax errors and configuration warnings in `mulang.config.json`,
diagnostic removal after correction or deletion, and restoration of disk
diagnostics after discarding unsaved changes. Confirm that JSON coloring and
schema-aware editing still work and that unrelated JSON files are unaffected.

### MuIR checks

Use the existing files under
`tests\MuLang.IR.Tests\Serialization\Fixtures`, starting with `complete.muir`
(version 1), `diagnostic-attributes-v2.muir`, `object-properties-v2.muir`,
`recursive-types-v2.muir`, and `exception-regions-v2.muir`. Edit copies when
checking invalid input; do not overwrite the fixtures.

1. Open a `.muir` directly with **File > Open > File**, then through **File >
   Open > Folder**. Repeat in Solution View with a project-included file and
   an existing file added as a link. Confirm activation, the reused file icon
   where the shell displays it, and that no host configuration is required.
2. Check immediate lexical coloring before server readiness and semantic
   coloring afterward, in light and dark themes. Check comments, escaped
   strings, opcodes, type/slot/block/region IDs, and numbers. Reference-like
   text inside comments or string payloads must not gain navigation or hover.
3. Hover IDs for classified types/signatures and available original names.
   Use **Go To Definition** and **Find All References** on declared type,
   slot, block, and explicit region IDs. Results must stay in the same MuIR
   document and, for local IDs, the same function, even for linked files or
   another open MuIR file with identical IDs. Check read/write occurrence
   rendering without altering normal MuLang solution-wide references.
4. Verify actual document-symbol presentation in each target VS version.
   Inspect the editor navigation controls and, where available, **View >
   Other Windows > Document Outline**. Record the concrete surface used,
   whether hierarchical children appear, and navigation to types/functions,
   slots, blocks, and explicit version-2 regions. Version-1 implicit regions
   must not appear as declarations. A protocol response alone does not prove
   an outline is visible; unavailable UI is an unverified feature, not a pass.
5. Use gutter outlining controls or **Edit > Outlining > Toggle Outlining
   Expansion** on multiline functions, blocks, object-property lists, and
   attribute bodies. Confirm collapse/expand and the range boundaries
   actually presented by VS. Record whether folds come from the language
   server or lexical fallback; generic brace folding alone does not prove
   that LSP structural folds are rendered.
6. Make an unsaved edit that keeps a document readable and confirm updated
   hover, references, symbols, and folds. Then break a required delimiter or
   use an unsupported format version: lexical coloring must remain, while
   semantic hover, navigation, references, symbols, and structural folds
   disappear without MuIR diagnostics. Lexical folds may remain. Correct
   the edit and confirm recovery; repeat after close/reopen.
7. Open `.mu`, `.mulang`, `.muir`, and `mulang.config.json` together. Confirm
   MuLang diagnostics/configuration updates and solution-wide references
   still work and do not compile or publish diagnostics for MuIR.

Record VS edition/version, opening mode, fixture, and observed symbol/folding
presentation with the verification results. Actual VS outline and folding
remain manual checks until performed; neither a build nor static asset checks
establishes them. Do not add a VS-specific bridge unless a reproducible client
limitation is verified and the resulting scope expansion is reviewed first.
