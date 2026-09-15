---
description: C# coding practices and conventions.
applyTo: '**/*.cs'
---

# C\#

Use compact `namespace` declarations.

---

Sort `using` directives in alphabetical order (no `System` first).

---

Put top-level types in their own files, and name the file after the type. Of course, the only exception are `file`-scoped types.

---

Mark lambdas and local functions as `static` whenever possible.

---

Leverage local functions for small helper functions.

---

Put `[MethodImpl(MethodImplOptions.AggressiveInlining)]` on small methods and local functions when possible, especially in hot paths.

---

Prefer `throw new ArgumentException` (or similar argument-related exceptions) over `ArgumentException.ThrowIfWhatever` (or similar argument-related methods).

---

Prefer explicit types over `var` for local variables, except for lengthy types or when mandatory (e.g. anonymous types, LINQ queries, `var` pattern, etc.).

---

Prefer interface collections over concrete collections, unless the concrete collection is required for some reason.

Among the interface collections, choose the most appropriate one for the use case.
For example:
- `IReadOnlyCollection<T>` over `IEnumerable<T>`, when count is required or when the underlying instance is not lazy;
- `IReadOnlyList<T>` over `IReadOnlyCollection<T>`, when indexing is required;
- writable collections over read-only collections, when the collection is expected to be modified.

All rules above can be relaxed if other APIs make it necessary or more convenient to use a different collection type.

Choosing the correct collection type is difficult and sometimes is just a matter of taste. In practice, try to mimic existing patterns in the codebase and choose the minimal necessary collection type.

# C# XML Documentation

Do not touch functional code. Only add or modify XML documentation comments.

---

Do not touch generated files (e.g. `*.g.cs`, `*.designer.cs`).

---

Do not alter the `GenerateDocumentationFile` project property or other project settings related to XML documentation.

---

Unless told otherwise, write documentation only for symbols with effective visibility outside the solution. In other words, a `public` or `protected` member declared inside an `internal` type (or otherwise not exposed beyond the assembly) should not be documented; the same applies to nested members whose chain of containing types is not public.

---

If a method has non-obvious semantics, deduce it by reading the implementation; if it remains ambiguous, use a factual and neutral description instead of inventing details.

---

Every parameter, type parameter, and non-void return value must have its own tag, unless the symbol is omitted.

---

Do not leave "contentful" tags empty or with placeholders (`TODO`, `...`).

---

Keep descriptions concise, factual, and within the component's domain.

---

Do not restate the signature or list parameters in the `<summary>`.

---

Prefer meaning over mechanical restatement. In other words, write plain descriptions like "Gets or sets the Foo" only if there is nothing else to add value.

---

If a pattern is already used elsewhere in the documentation, reuse it. This implies that, if a new shared style is established, you may ask me whether you can proceed updating existing documentation to match it.

## Style guide

- **Language**: English, third person, every sentence ends with a period.
- **Types**:
  - data classes/structs and data interfaces → `<summary>Represents ...</summary>`.
  - static extension classes → `<summary>Provides extension methods for ...</summary>`.
  - behavior/service interfaces → `<summary>Represents an interface for ...</summary>`.
- **Properties**: `Gets {or sets} the ...`; for booleans `Gets {or sets} a value indicating whether ...`.
- **Constructors**: keep the `<summary>` concise and non-redundant. The base pattern is `Initializes a new instance of the <see cref="TypeName" /> {class|struct}.`, optionally with a very brief note on the purpose of the overload when it adds real information. Do not list or paraphrase the parameters in the summary: the details of the individual parameters are in the `<param>` tags, not in the description. Avoid long sentences that repeat the signature.
- **Methods**: third person verb form: `Determines whether ...`, `Converts ...`, `Parses ...`, `Adds ...`, `Gets ...`, `Creates ...`.
- **Cross-references**: `<see cref="TypeName" />` with space before the self-closing tag `/>`. The same for `<inheritdoc />`.
- **Method details**:
  - `<param name="x">Description.</param>` for each parameter.
  - `<typeparam name="T">Description.</typeparam>` for each type parameter.
  - `<returns>...</returns>` for each non-void.
  - `<exception cref="ExType">Thrown when ...</exception>` for each directly thrown and documentable exception.
- **Boolean returns**: `<returns><c>true</c> if {description}; otherwise, <c>false</c>.</returns>`.
- **Chaining**: when the method returns the same instance provided as input (typical of fluent/builder patterns or extension methods that return the object itself for chaining), make it explicit in the `<returns>` — e.g. `<returns>The same <paramref name="x" /> instance, for chaining.</returns>` — instead of generically describing a new value.
- **`Try`/out pattern**: `<param name="result">When this method returns, contains ...</param>`.
- **Inline code**: use `<c>null</c>`, `<c>true</c>`, `<c>false</c>`, `<c>MemberName</c>`. In `<code>` blocks, generics should be written as `&lt;` `&gt;` (e.g. `ICollection&lt;string&gt;`).
- **`<inheritdoc />`**: use this tag for overrides and implementations of members already documented elsewhere — `Equals`, `GetHashCode`, `CompareTo`, `ToString`, `TryFormat`, standard operators, and interface members whose documentation is on the interface. Do not repeat the description, unless there is additional information.
- **`<remarks>`**: use this tag to provide useful information and non-obvious details relevant to the caller, without delving into implementation specifics and without redundancy with respect to other parts of the symbol documentation. For example: parameter syntax, common pitfalls, scenarios, conditions, side effects, and so on.
- **`<example>`/`<code>`**: use only in cases where it clarifies a non-trivial usage.
- **Non-browsable symbols**: visible (w.r.t access-wise visibility) symbols marked with `[EditorBrowsable(EditorBrowsableState.Never)]` are documented anyway.
- **Indentation**: respect the indentation of the documented symbol (the `///` aligns with the symbol).
