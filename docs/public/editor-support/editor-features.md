# Editor features

These features describe the current source tree. See
[release availability](index.md#release-availability) before choosing an
extension version.

## Diagnostics

Opening or changing a MuLang document publishes compiler diagnostics. Closing
it clears those diagnostics. Every diagnostic retains its MuLang diagnostic
code, severity, message, and source span.

MuLang stores source spans in Unicode scalar offsets. The language server
converts them to zero-based UTF-16 line and character positions, keeping
Visual Studio squiggles aligned after supplementary Unicode characters.

Diagnostics use the document's effective
[workspace configuration](configuration.md), including its language profile
and host environment.

## Hybrid highlighting

TextMate provides immediate lexical highlighting for keywords, including the
MuLang 1.2 `type` declaration keyword and `primitive` intrinsic type, literals,
strings, operators, and punctuation. Object-property `$`, `?`, `:`, and `=`
tokens retain those ordinary classifications. Lexical highlighting remains
active while the language server starts and acts as a fallback if the server
is unavailable.

The compiler supplements TextMate with binding-based semantic classifications
for named types, functions, parameters, variables, and object properties.
These include source declarations and references, host-provided symbols,
explicit property types, and inline or nested object-type declarations.
Declaration and read-only modifiers are retained where supported by the client.
Intrinsic type keywords remain lexical TextMate classifications.

### Property highlighting

Semantic property highlighting covers object-type and object-literal
declarations, member access, statically resolved literal-key access, property
tests, array `length`, and built-in `error` properties. Dynamic member names
remain property-colored but do not participate in navigation.

Visual Studio uses its native `class name`, `method name`, and `property name`
classifications for named structured types, functions, and properties. Colors
therefore follow the active theme and Fonts and Colors settings independently
from parameters and variables.

## Semantic hover

Hover shows resolved signatures for variables, parameters, functions, types,
and properties, both at declarations and references. Configured host symbols
use the same compiler binding as diagnostics and semantic tokens.

Signatures include inferred types, read-only and optional modifiers, function
parameters and return types, and structural type properties. Array `length`,
literal property indexing, and exception-handler symbols are supported.
Each item has an editor category such as `variable`, `parameter`, `function`,
`type`, `property`, `literal`, `expression`, or `operator`; declaration
keywords are not part of the signature.

Object bodies with at least two properties and function signatures with at
least two parameters place each entry on its own line with two spaces of
indentation per multiline nesting level. Empty and single-entry lists remain
compact. Named types remain named inside signatures, including recursive types.

Declaration modifiers also trigger the associated symbol's hover: `$` for
variables, and `$` and `?` for object properties in type bodies and object
literals. Modifier tokens have separate hover-only ranges; semantic coloring,
highlights, definitions, and references continue to cover only symbol names.
Whitespace between the name and modifiers has no hover. Nullable-type `?`
and array read-only `$` do not trigger this symbol hover.

Locals whose type cannot be inferred because of an earlier source error use
`<unresolved>` as the compiler recovery type. Parameters whose declared type
cannot be resolved use the same recovery type. Undefined variable names show
`name: <unresolved>` without becoming navigable symbols or receiving semantic
coloring.

Scalar literals show their source spelling and bound type. Undefined calls
show `name(...): <unresolved>`, missing properties show
`name: <unresolved>`, and valid dynamic properties show their `unknown?` result
type. Dynamic element access shows only the symbol, literal, or operator
immediately under the pointer, without adding the complete access expression
and its result type.

Unary and binary arithmetic, logical, relational, equality, identity, and
bitwise operators show function-shaped signatures based on their bound operand
and result types. Invalid operators use `<unresolved>` as their result type.
A sign lexed as part of a numeric literal remains part of the literal hover;
unary `+` and `-` applied to expressions use operator hover.
Null coalescing uses `??(left, right): result`. Conditional expressions use
`?:(condition, trueBranch, falseBranch): result` on both the `?` and `:` tokens.
`is`, `has`, `as`, assignment, and removal operators do not have operator hover.
When a call target is not a simple function name, its own expression hover is
preserved without synthesizing a function signature.

Whitespace, unsupported atoms, and unopened or closed documents have no hover.
Document and configuration changes are reflected in subsequent requests.

### Visual Studio Quick Info

The Visual Studio extension replaces the generic LSP hover presenter with
native asynchronous Quick Info. It displays each categorized item as a block
with an italic category prefix and applies theme-aware classifications to the
individual signature parts rather than coloring the entire signature by the
hovered symbol's category.

Names use their semantic roles; intrinsic types and keyword literals use
keyword classification, strings and numbers use their literal classifications,
and operators and punctuation retain separate classifications. Line breaks and
indentation are preserved. Other clients continue using standard LSP hover.

## Semantic navigation

Document highlights use compiler symbol identity rather than matching source
text. Moving the caret onto a resolved symbol highlights its declaration and
references in the current document, with separate kinds for reads and writes.
Shadowed locals and same-named properties on different object types are kept
separate.

Object literal properties share navigation identity with matching properties
of their contextual structured object type. Their inferred types, mutability,
optionality, and hover signatures remain those of the literal. Additional
properties without a contextual match retain their own identity. Nested
literals follow their effective context, including explicit property type
annotations; structural assignments between existing objects do not merge
property identities.

### Control-flow highlights

Control-flow keywords use separate association groups:

- an `if`/`else if`/`else` chain highlights every directly associated `if` and
  `else`;
- `try`, `catch`, and `finally` highlight the clauses of the same protected
  statement;
- `for` and `while` highlight every `break` and `continue` that resolves to
  that loop, including multi-level transfers;
- `func` highlights every `return` and `throw` in that function, and selecting
  one of those transfers highlights the complete function group.

Top-level `return` and `throw` have no function group. Invalid loop transfers
are not associated with a loop. A valid group is returned even when it contains
only its anchor keyword.

### Definitions

Definition navigation resolves locals, catch variables, parameters, user
functions, source types, and source or inferred object properties. Configured
host symbols, intrinsic types, dynamic properties, array `length`, and built-in
`error` properties have no source definition.

### References

Find All References searches the current document for source-local symbols.
Configured host symbols and built-in properties are searched across every
`.mu` and `.mulang` file in the workspace, including unopened files. Open
documents take precedence over disk content, and each document uses its own
effective workspace configuration.

In Visual Studio Solution View, each search uses an up-to-date list of MuLang
files included in the solution or its loaded projects, including linked files
outside the solution directory. Files that are merely present on disk or open
in the editor are not included. This excludes ordinary `bin` and `obj` output
copies without excluding files explicitly included from those directories.
Folder View retains recursive workspace discovery.

Visual Studio reference results distinguish declarations, reads, writes, and
other references using the compiler's occurrence roles. Other LSP clients
continue to receive standard locations.

Intrinsic type tokens have workspace-stable identities for highlights and
references. Literals and symbols whose inferred type is intrinsic are not
included unless an intrinsic type token is explicitly present. The well-known
`code`, `message`, `cause`, and `data` properties in `throw` object literals
share identity with the corresponding built-in properties of `error`.

All navigation positions and ranges use UTF-16. Unknown documents, unresolved
symbols, and non-symbol positions return no result.
