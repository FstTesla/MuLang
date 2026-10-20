# Editor features

See [release availability](index.md#release-availability) for the versions
covered by this documentation.

## Diagnostics

Errors and warnings appear while editing, with a diagnostic code, message,
and highlighted source location. They use the document's
[workspace configuration](configuration.md), including its language profile
and host environment.

## Highlighting

Syntax coloring distinguishes keywords, literals, operators, and punctuation.
Type-aware coloring also distinguishes types, functions, parameters, variables,
and properties, including configured host symbols.

Colors follow the Visual Studio theme and Fonts and Colors settings. Basic
syntax coloring remains available while the language server starts or is
unavailable.

## Hover information

Hover over a symbol to see its type or signature, including inferred types,
read-only and optional modifiers, function parameters, and return types.
Hover also provides type information for literals and supported operators.

Visual Studio displays this information through Quick Info, with syntax
coloring and multiline signatures where appropriate.

## Highlighting related occurrences

Place the caret on a symbol to highlight its declaration and uses in the
current document. Reads and writes are distinguished, and unrelated symbols
with the same name are kept separate.

Selecting a control-flow keyword also highlights related branches, handlers,
or transfers in the same conditional, exception statement, loop, or function.

## Go to Definition

Go to Definition navigates to declarations of variables, parameters,
functions, types, and properties in MuLang source. Property names in object
initializers can navigate to the corresponding declaration in their contextual
type.

Host-provided symbols and built-in members have no MuLang source definition.
Dynamic or unresolved properties cannot be navigated.

## Find All References

References to source-declared symbols are searched within their document.
Host-provided symbols and built-in types and members can be searched across
MuLang files in the workspace, including unopened files. Unsaved changes in
open documents are included.

In Visual Studio Solution View, searches cover MuLang files included in the
solution or its projects, including linked files. In Folder View, searches
cover MuLang files under the workspace folders.
