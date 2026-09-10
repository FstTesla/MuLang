# General

Be concise. Do not repeat yourself. Avoid unnecessary explanations and enthusiastic language.

---

In conversations with multiple phases, make sure to re-read all involved files, so that you acknowledge any manual changes made by the user.

---

Do not generate examples, unless explicitly told to do so.

---

Do not generate comments, unless explicitly told to do so, or unless what is being done is very hacky (not reasonably self-explanatory).

---

Avoid code duplication and reuse existing code as much as possible.
If existing code can be changed or extracted to cover the request, ask me whether I want to proceed that way.

---

Match the style of the existing code as closely as possible.

---

Some specific style guidelines:
- Write "terminating" control statements (`return`, `break`, `continue`, etc.) on separate lines.
- Do not use hungarian notation or leading underscores for variable names.
- Always use braces for flow control statements, unless it is an `if` with a single "terminating" statement as body.
- Use blocks for `case` statements if they declare variables.
- For 1-statement lambda bodies, prefer expression bodies when returning something, and statement bodies otherwise (assignments, method calls, etc.).
- Avoid redundant parentheses, both in expressions and for lambda parameters.

---

When a task can be performed in multiple ways, ask me which way I prefer before proceeding.

---

When a task is open for further steps, ask me whether I want you to proceed with them.

---

Commented code should not have leading whitespace, except as indentation. On the contrary, textual comments should have a leading space.
Some examples:
```ts
    // Description of something
```

```ts
    //const twoPi = Math.PI * 2;
```

```ts
    const twoPi = Math.PI * 2; // This is 2 * pi
```

```ts
    console.info('Loading');
    //if (condition) {
    //  console.error('Failed!');
    //}
    console.info('Loaded!');
```

# TypeScript and TSX

Use `interface` instead of `type` whenever possible.

---

Use `const` for variables that are not reassigned, and `let` for variables that are reassigned. Do not use `var`.

---

Prefer template literals rather than plain string concatenation, when it improves readability.

---

Prefer using optional chaining and nullish coalescing when appropriate to handle nullable values.

---

Use `unknown` or `never` instead of `any`, whenever possible.

---

In non-`async`, non-`Promise` function bodies, prefix `async` function calls with `void` to mark them explicitly and to prevent warnings.

---

When writing a multiline ternary expression in TSX, place the condition on its own line, and break the lines before the `?` and `:` operators, but not after them.
For example:
```tsx
condition
  ? <TrueComponent />
  : <FalseComponent />
```

# React and Fluent

Properly use React hooks, in particular:
- memoization-related hooks (`useMemo`, `useCallback`, `memo`);
- `useEffectEvent`.

---

Prefer callback-based invocations of `setState` when the new state depends on the previous state. Do not put side effects in `setState`.

---

Avoid inline `style` attributes and prefer CSS classes based on Fluent's `makeStyles`.

---

Leverage Fluent's `tokens` in CSS styles whenever possible, except for `0` dimensions.

# C\#

Use compact `namespace` declarations.

---

Put top-level types in their own files, and name the file after the type. Of course, the only exception are `file`-scoped types.
