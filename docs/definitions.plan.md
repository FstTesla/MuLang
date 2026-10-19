# Definitions, document highlights, references, and property classification plan

## Status

Implemented for `0.4.0-beta.2`.

The increment covers:

- a shared compiler semantic-occurrence model;
- a Visual Studio-specific property-name classification;
- same-document symbol highlighting under the caret;
- navigation to source definitions;
- workspace-wide reference discovery.

Native classified Visual Studio Quick Info remains a separate VSIX integration
step. It should consume the same semantic model introduced here, but it is not
part of this implementation increment.

## Confirmed decisions

The following decisions are fixed for this plan:

1. `textDocument/references` searches the complete workspace, including MuLang
   files that are not open in the editor.
2. Property identity is semantic. Two properties are related only when binding
   resolves them to the same property; matching names alone are insufficient.
3. Dynamic properties on `object` and open objects have no navigable identity
   and do not participate in document highlights, definitions, or references.
4. Standard LSP clients continue to receive the `property` semantic-token type.
5. Visual Studio receives the native `property name` classification, matching
   the classification used for C# properties and respecting the active theme
   and Fonts and Colors settings.
6. Source-declared types, functions, variables, parameters, catch variables,
   and properties remain local to one MuLang document. Only configured host
   symbols can have references in multiple documents.
7. The compiler and language server remain independent of Visual Studio SDK
   assemblies and LSP protocol types.

## Current state

The binder already resolves the information needed by most of the feature:

- locals and catch variables use `LocalSymbol`, including declaration spans;
- function parameters use `UserParameterSymbol`, including declaration spans;
- user functions use `UserFunctionSymbol`, including their declarations;
- source types are represented by source `ObjectTypeSymbol` instances;
- globals and provider functions retain their configured provider identifiers;
- member and literal-key element access retain a resolved
  `ObjectPropertySymbol` when the property is statically known;
- semantic classification already distinguishes `Type`, `Function`,
  `Parameter`, `Variable`, and `Property`;
- hover and semantic tokens already share the bound-tree traversal in
  `SemanticClassifier`.

The current model is insufficient for navigation because:

- semantic classifications expose only kind, modifiers, and span;
- hover formats symbols during the classifier traversal instead of consuming a
  reusable semantic document;
- source-type declarations and property declarations are not represented by a
  general definition model;
- object-property symbols do not identify their owning object type;
- property tests do not retain a resolved property;
- literal-key element access resolves a property for hover but is not emitted
  as a property semantic token;
- the language server stores only open documents and has no workspace source
  discovery or semantic cache;
- Visual Studio currently receives `property` rather than the native
  `property name` classification.

## Semantic model

### Symbol identity

Introduce an internal compiler symbol-identity abstraction used by every
editor feature. Identity must not be inferred from source spelling.

The identity categories are:

| Symbol | Identity |
| --- | --- |
| Local or catch variable | The bound `LocalSymbol` instance. |
| Function parameter | The bound `UserParameterSymbol` instance. |
| User function | The bound `UserFunctionSymbol` instance. |
| Source type | The source `ObjectTypeSymbol` instance. |
| Anonymous object property | The owning anonymous object-type instance and property instance. |
| Source-type property | The owning source-type instance and property instance. |
| Configured global | Symbol kind plus provider ID. |
| Configured function | Symbol kind plus provider ID. |
| Configured type | Symbol kind plus provider type ID. |
| Configured property | Configured owner type ID plus property name. |
| Array `length` | A dedicated built-in property identity. |
| `error` property | A dedicated built-in identity per property name. |

Reference equality is sufficient within one binding result. Provider IDs and
owner type IDs form stable keys when occurrences from independently bound
workspace documents must be compared.

Do not add source locations or compiler-specific identity to public
`MuLang.Core` symbols. Keep declarations and navigation metadata in
`MuLang.Compiler`.

### Occurrences

Represent every resolved semantic occurrence independently from its editor
projection. Each occurrence must contain:

- symbol identity;
- semantic kind;
- exact source span;
- occurrence role;
- semantic-classification modifiers;
- display information required by hover and later Quick Info;
- an optional source definition span.

Occurrence roles are:

- `Declaration`;
- `Read`;
- `Write`;
- `Reference` for symbols where read/write does not apply.

Assignments and property removals are writes. Value reads are reads. Type and
function references use the neutral reference role. Declarations remain
declarations rather than being treated as writes.

The semantic model must retain occurrences even when later optimization removes
their bound expressions. Editor behavior is based on binding, not optimized IR.

### Property binding

Record the canonical property identity while binding:

- map every object-type property syntax to the created property symbol and its
  owner type;
- map every object-literal property syntax to the property symbol in the
  resulting object type;
- retain the resolved property on member access;
- retain the resolved property on literal-key element access;
- extend bound property tests to retain the resolved property when the key is a
  compatible string literal;
- assign built-in identities to array `length` and statically known `error`
  properties.

Full object literals define their own structural properties even when an
expected named type is available. Their property declaration therefore remains
distinct from the expected type's property declaration. Subsequent accesses
resolve according to the actual bound type at the access site.

Dynamic member access, computed keys, missing properties, and recovery nodes
may still receive property syntax coloring where appropriate, but they must not
receive a symbol identity.

### Compiler API

Replace the editor-specific traversal outputs with one reusable semantic
document result produced from a single binding:

- materialized semantic occurrences ordered by source span;
- lookup of the smallest resolved occurrence containing a scalar offset;
- lookup of all occurrences with the same identity;
- lookup of the source definition for an occurrence;
- existing diagnostics and source line map.

Expose only immutable, runtime-independent result types from
`MuLang.Compiler`. Do not expose binding-layer symbol classes.

The public surface should support the language server without requiring one
compiler invocation per editor feature. It should allow:

- semantic classifications to be projected from occurrences;
- hover content to be projected from the selected occurrence;
- document highlights to be projected from matching occurrences;
- definition to be projected from the occurrence's definition;
- references to compare stable external symbol keys across semantic documents.

Preserve `ClassifySemantically` and `GetHover` as compatibility entry points.
Implement them as projections over the new semantic document rather than
maintaining separate traversals. Add the required Public API baseline entries
for any new public result types and methods.

## Property semantic classification

### Compiler behavior

Continue emitting `SemanticClassificationKind.Property` for:

- source and inline object-type property declarations;
- object-literal property declarations;
- statically resolved member access;
- statically resolved literal-key element access;
- statically resolved property tests;
- array `length`;
- statically known `error` properties.

Unresolved or dynamic member names should remain property-colored when they are
syntactically in a member-name position, but they must not participate in
navigation.

For literal-key access and property tests, classify only a statically resolved
string-literal key. Computed expressions retain their normal expression
classification.

### LSP behavior

Keep the standard semantic-token legend unchanged for non-Visual Studio
clients:

1. `type`;
2. `method`;
3. `parameter`;
4. `variable`;
5. `property`.

When the server runs with Visual Studio classifications, advertise:

1. `class name`;
2. `method name`;
3. `parameter`;
4. `variable`;
5. `property name`.

The numeric token index remains stable, so encoded semantic-token data does not
change when only the legend string changes.

Do not add a custom VSIX classification or a custom fixed color. The native
`property name` classification supplies a color distinct from types, methods,
parameters, and locals while following the user's Visual Studio theme and
Fonts and Colors settings.

## Document highlights

### Capability

Advertise `documentHighlightProvider: true` and handle
`textDocument/documentHighlight`.

The request operates only on an open document. Unknown or closed documents and
positions without a resolved symbol return `null`.

### Result selection

Resolve the semantic occurrence under the requested UTF-16 position and return
all occurrences with the same identity in the current document.

Map occurrence roles to LSP highlight kinds:

| Occurrence role | LSP kind |
| --- | --- |
| Read | `Read` |
| Write | `Write` |
| Declaration or neutral reference | `Text` |

Return ranges in source order without duplicates. Visual Studio owns the
background color and theme rendering.

Names that merely have the same text must not be highlighted together. This
includes:

- shadowed locals;
- parameters and locals with the same name;
- properties with the same name on different object types;
- source symbols and configured host symbols with the same spelling;
- dynamic properties matching a statically declared property name.

## Definition navigation

### Capability

Advertise `definitionProvider: true` and handle `textDocument/definition`.

Return a single LSP `Location` when the selected occurrence has a source
definition. Return `null` when no definition exists.

### Supported definitions

Support navigation for:

- local and catch-variable declarations;
- function-parameter declarations;
- user-function declarations;
- source-type declarations;
- source and inline object-type property declarations;
- object-literal property declarations;
- references to all of the above.

Selecting a declaration may return that declaration's own location.

Return `null` for:

- configured globals, functions, and types;
- configured properties;
- array `length`;
- built-in `error` properties;
- dynamic or unresolved properties;
- intrinsic types and language keywords.

Definition ranges must select the identifier or quoted property-name token, not
the complete declaration.

## Workspace references

### Capability

Advertise `referencesProvider: true` and handle `textDocument/references`.

Honor `context.includeDeclaration`. Return an empty array when a symbol is
resolved but has no matching references in the requested scope. Return `null`
when the request position does not resolve to a symbol.

### Search scope

For document-local identities, search only the semantic document that owns the
selected symbol:

- locals and catch variables;
- parameters;
- user functions;
- source types;
- anonymous and source-defined properties.

For stable configured or built-in identities, search every `.mu` and `.mulang`
document in every applicable workspace folder:

- configured globals;
- configured functions;
- configured types;
- configured properties;
- array `length`;
- built-in `error` properties.

Nested workspace folders must not cause duplicate results. A document belongs
to the most specific containing workspace folder for configuration purposes.

### Workspace source discovery

Add a language-server workspace source service that:

- discovers `.mu` and `.mulang` files recursively under file-based workspace
  folders;
- supports multiple and nested workspace folders;
- canonicalizes file paths and URIs using the platform's path comparison;
- prefers the current in-memory snapshot for open documents;
- reads unopened documents from disk;
- skips files that disappear during discovery;
- reports unexpected file-access failures through the existing server error
  path instead of returning partial success-shaped data;
- observes request cancellation during enumeration, file reads, binding, and
  response generation.

Do not recursively scan non-file workspace URIs. For those workspaces,
reference discovery is limited to open documents.

Workspace scanning should not introduce a persistent background watcher in the
first implementation. Discover on demand and use a bounded cache keyed by:

- canonical document URI;
- open-document version, or disk last-write time and length;
- effective workspace-configuration generation.

Invalidate affected cache entries on:

- `didOpen`, `didChange`, and `didClose`;
- workspace-folder changes;
- pushed configuration changes;
- `mulang.config.json` changes;
- source-file changes reported through `didChangeWatchedFiles`.

If the client does not report source-file changes, disk metadata validation on
the next references request must prevent stale results.

### Configuration-sensitive matching

Bind every searched document with its own effective workspace configuration.
Stable provider identities must include both symbol kind and provider ID.
Configured property identities must include the configured owner type ID and
property name.

Symbols with the same language name but different configured IDs are unrelated.
An invalid configuration update continues to use the last valid configuration,
matching existing diagnostics and hover behavior.

## Language-server refactoring

Extract shared request helpers instead of duplicating hover logic:

- parse and validate text-document positions;
- convert UTF-16 LSP positions to scalar offsets;
- convert scalar spans to UTF-16 LSP ranges;
- retrieve and bind the effective semantic document;
- write locations and location arrays;
- normalize and compare document URIs.

Keep protocol serialization in `MuLang.LanguageServer`. The compiler returns
semantic data only.

The server's serialized state gate may remain for the initial implementation.
Workspace reference search must still honor cancellation so a long request does
not indefinitely block document synchronization.

## Implementation sequence

### Phase 1: shared semantic document

1. Define immutable symbol-key, occurrence-role, occurrence, definition, and
   semantic-document result types in `MuLang.Compiler`.
2. Extend binding metadata for source declarations and canonical properties.
3. Replace hover-specific callbacks in `SemanticClassifier` with a semantic
   document builder.
4. Project existing semantic classification and hover APIs from the shared
   result.
5. Preserve current diagnostics, semantic-token ordering, hover formatting,
   scalar spans, and Public API compatibility.

### Phase 2: complete property classification

1. Emit property occurrences for every supported property syntax.
2. Keep dynamic property coloring separate from navigable identity.
3. Change only the Visual Studio semantic-token legend entry from `property`
   to `property name`.
4. Retain the standard `property` token type for other clients.

### Phase 3: document highlights

1. Add the LSP capability and request handler.
2. Resolve the caret symbol from the shared semantic document.
3. Return same-document occurrences with `Text`, `Read`, and `Write` kinds.
4. Cover shadowing, property-owner identity, UTF-16 positions, and invalid
   request behavior.

### Phase 4: definition

1. Add the LSP capability and request handler.
2. Return exact source declaration locations for document-local symbols.
3. Return `null` for host, intrinsic, built-in, dynamic, and unresolved symbols.
4. Verify declaration self-navigation and quoted property ranges.

### Phase 5: workspace references

1. Add workspace source discovery and semantic-document caching.
2. Add the LSP capability and request handler.
3. Search only the owning document for source-local identities.
4. Search the complete workspace for configured and built-in identities.
5. Respect `includeDeclaration`, configuration hierarchy, unsaved snapshots,
   nested workspace folders, cancellation, and cache invalidation.

### Phase 6: documentation and Visual Studio validation

1. Update `docs/language-server.md` with capabilities, scope, and limitations.
2. Update `docs/roadmap.tmp.md` when the increment is complete.
3. Build the VSIX and verify that it contains the updated language server.
4. Manually verify native Visual Studio property colors and document-highlight
   background rendering under light and dark themes.
5. Manually verify definition navigation and Find All References from source and
   configured symbols.

## Test plan

### Compiler tests

Add focused tests for:

- occurrence identity across declarations and references;
- nested-scope and function-scope shadowing;
- local, parameter, catch-variable, function, and type definitions;
- read and write roles for assignment and removal;
- properties with equal names on unrelated object types;
- source, inline, anonymous, configured, array, and `error` properties;
- member access, literal-key element access, and property tests;
- dynamic and unresolved properties without navigation identity;
- quoted property names and escaped string-literal spans;
- references retained despite constant folding;
- Unicode scalar spans and malformed source recovery;
- unchanged semantic classifications and hover content.

### Language-server tests

Add protocol tests for:

- advertised highlight, definition, and references capabilities;
- Visual Studio `property name` versus standard LSP `property`;
- UTF-16 request positions and response ranges;
- read/write/text document-highlight kinds;
- definition locations and `null` results;
- `includeDeclaration` behavior;
- unsaved open-document text taking precedence over disk;
- references in unopened workspace documents;
- multiple and nested workspace folders;
- configuration overrides and last-valid configuration retention;
- same-name provider symbols with different IDs;
- file creation, change, deletion, and cache invalidation;
- non-file workspace folders;
- missing, malformed, negative, and out-of-range request positions;
- unknown and closed documents;
- cancellation during workspace discovery.

### Regression validation

Run:

- compiler editor and hover tests;
- the complete language-server test project;
- the Visual Studio extension build;
- existing semantic-token and diagnostics tests affected by the shared semantic
  traversal.

Use `--no-restore` for .NET validation commands.

## Completion criteria

The increment is complete when:

- all semantic editor features consume one compiler semantic-occurrence model;
- Visual Studio renders object properties with native `property name`
  classification;
- placing the caret on a resolved symbol highlights only semantically matching
  occurrences in the current document;
- definition navigation reaches every supported source declaration;
- Find All References searches unopened files across the configured workspace;
- dynamic and same-named unrelated properties never produce false matches;
- UTF-16 positions and scalar compiler spans remain correct;
- document edits and configuration changes cannot return stale navigation
  results;
- compiler, language-server, and VSIX validation succeeds.
