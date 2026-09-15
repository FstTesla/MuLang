---
description: General conversation behavior and coding practices.
applyTo: '**'
---

# Conversation behavior

Preserve per-file line endings and encoding. For new files, deduce them from the rest of the solution; as a fallback, assume CRLF and UTF-8 without BOM.

---

Be concise. Do not repeat yourself. Avoid unnecessary explanations and enthusiastic language.

---

After user prompts, make sure to re-read all involved files, so that you acknowledge any manual changes made by the user.

---

When a task can be performed in multiple ways, ask me which way I prefer before proceeding.

---

When a task is open for further steps, ask me whether I want you to proceed with them.

# Coding practices and style

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

The guidelines above can be applied to any C#-like or JavaScript-like language, and can be extended as needed for other similar languages.

---

Commented code should not have leading whitespace after the comment delimiter, except when preserving indentation. On the contrary, textual comments should have a leading space.
Some examples:

- Textual comment has a leading space
  ```ts
  // Description of something
  ```
- Code comment has no whitespace
  ```ts
  //const twoPi = Math.PI * 2;
  ```
- Inline textual comment has a leading space
  ```ts
  const twoPi = Math.PI * 2; // This is 2 * pi
  ```
- Code comment has no leading whitespace, except when preserving indentation
  ```ts
  console.info('Loading');
  //if (condition) {
  //  console.error('Failed!');
  //}
  console.info('Loaded!');
  ```
