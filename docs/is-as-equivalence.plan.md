# `is`/`as` Equivalence Plan

## 1. Objective

Make `is` and `as` share the same runtime-conformance semantics:

> Excluding operational failures, `value is T` evaluates to `true` if and only if `value as T` completes successfully.

A successful `as` cast MUST return the original runtime value without changing its representation. For objects and arrays, it MUST also preserve logical identity.

The change deliberately separates:

- implicit and contextual conversions, which may transform a value;
- checked casts, which only validate runtime conformance and preserve the value.

Operational failures such as cancellation, execution-budget exhaustion, traversal-depth exhaustion, environment incompatibility, or provider failure are outside the equivalence guarantee.

## 2. Confirmed semantic decisions

### 2.1. Numeric behavior

Implicit numeric conversions remain available where required by assignment, argument passing, return values, common-type computation, and operators.

The `as` operator does not perform numeric representation changes:

- an `int` does not conform to `float`;
- a `number` conforms to `int` only when its concrete runtime representation is already `int`;
- a `number` conforms to `float` only when its concrete runtime representation is already `float`;
- a value of static type `unknown` follows the same concrete-runtime-representation rules.

Consequently, the existing implicit `int`-to-`float` conversion remains part of the language, but `int as float` becomes invalid.

The planned standard-library `truncateToInt(float): int` operation remains the intentional mechanism for truncating a floating-point value toward zero. It is not semantically identical to the current `number as int` behavior, which accepts only floating-point values that are already integral.

### 2.2. String behavior

The `as` operator no longer converts primitive values or `null` to `string`.

String concatenation remains the contextual conversion mechanism for primitive values and `null`. Objects and arrays continue to have no intrinsic string conversion.

A cast from `unknown` to `string` succeeds only when the runtime value is already a string.

### 2.3. Nullable behavior

Checked removal of nullability remains supported because it validates and preserves the runtime value:

- a non-null value can be cast from `T?` to `T`;
- `null` fails a cast to a non-null type;
- `null` conforms to nullable target types;
- a cast to a nullable target preserves either the original non-null value or `null`.

### 2.4. Object and array behavior

Structured-object and array casts remain shape-based checked casts.

Array conformance continues to require:

- read capability for `T[]$`;
- write capability for `T[]`;
- recursive conformance of every current element to `T`.

Successful array casts preserve storage and logical identity. They do not establish a permanent invariant against later mutation through another alias.

Object conformance and conversion MUST use the same structural validation path. The current difference between the shallow `as object` check and the deep `is object` check must be removed.

## 3. Specification changes

### 3.1. Types

Update `docs/public/language/07-types.md` to clarify that:

- `number` has no dedicated runtime representation;
- runtime conformance to `int` or `float` depends on the concrete numeric representation;
- runtime conformance does not perform numeric promotion.

### 3.2. Assignability and conversions

Restructure `docs/public/language/08-assignability-and-conversions.md` so that implicit conversions, contextual conversions, and checked casts are distinct concepts.

The numeric-conversion section must:

- retain implicit `int`-to-`float`, `int`-to-`number`, and `float`-to-`number` conversions;
- remove the transforming explicit `number`-to-`int` and `number`-to-`float` rules from `as`;
- state that narrowing from `number` is a checked runtime-representation test.

The string-conversion section must:

- remove general explicit conversion to `string`;
- retain contextual conversion performed by string concatenation;
- retain the existing culture-independent formatting rules used by concatenation.

The checked-cast section must specify that:

- `as` validates runtime conformance;
- a successful cast preserves the value and runtime representation;
- a failed cast produces a MuLang runtime error at the `as` source span;
- `is` is the general predicate for predicting semantic cast success;
- `is` and `as` use the same conformance operation;
- the equivalence excludes operational failures.

### 3.3. Operators

Update `docs/public/language/11-operators.md` to define:

- `value is T` as the non-throwing semantic test for the corresponding checked cast;
- `value as T` as the checked form that returns the unchanged value or reports a failed-cast runtime error;
- concrete numeric conformance without `int`-to-`float` promotion;
- shared shape and capability rules for object and array operands.

### 3.4. Runtime errors

Update `docs/public/language/15-runtime-errors.md` to remove the statement that no general predicate can determine whether `as` succeeds.

The section on intentionally non-preventable errors must distinguish semantic cast failure from operational failures. After this change, a failed checked cast is preventable through the corresponding `is` test when both operations observe the same unchanged runtime value.

### 3.5. Intermediate representation and language profiles

Review `docs/public/language/19-portable-intermediate-representation.md` and `docs/public/language/18-language-profiles.md` for terminology that treats `as` as a general transforming conversion.

The IR specification must require type tests and checked casts to use equivalent runtime-conformance rules while allowing implicit conversion instructions to remain representation-changing where specified.

## 4. Type-system and binder changes

### 4.1. Separate conversion classification from cast eligibility

Revise `TypeRelations` so that `ClassifyConversion` is no longer sufficient to decide whether an `as` expression is valid.

Introduce or extract a relation that determines whether two static types can participate in a checked runtime-conformance cast. It must account for:

- identity and nullable casts;
- casts from `unknown` and `unknown?`;
- `number` to either concrete numeric type;
- object shape casts;
- array shape and capability casts;
- casts to nullable forms of eligible targets.

It must reject transforming operations such as:

- primitive or `null` to `string`;
- statically known `int` to `float`;
- statically known `float` to `int`.

Implicit assignment conversion classification must retain its current numeric promotions.

### 4.2. Bind `as` using cast eligibility

Update `Binder.BindConversionExpression` to use the checked-cast relation rather than accepting every identity, implicit, or checked conversion classified by the general conversion relation.

The bound representation must continue carrying enough information for lowering and IR validation to distinguish:

- implicit conversions inserted by the compiler;
- source-level checked casts.

If the existing `ConversionKind` abstraction cannot express this distinction clearly, replace or extend it rather than encoding the new behavior through special cases in the binder.

### 4.3. Bind `is` consistently

Keep `is` available for every non-void operand and non-void tested type unless a narrower static restriction is required for consistency with `as`.

Determine whether statically impossible tests should:

- remain valid and evaluate to `false`; or
- receive a diagnostic matching an unavailable `as` cast.

The selected rule must preserve the stated equivalence for every pair of well-formed corresponding expressions and be documented explicitly.

## 5. Runtime changes

### 5.1. Establish one conformance operation

Extract or designate one runtime operation as the authoritative deep-conformance check.

Both `DotNetRuntimeOperations.TypeTest` and checked `ConvertValue` execution must call this operation with identical:

- nullable handling;
- primitive representation checks;
- object shape checks;
- array capability and element checks;
- traversal-budget accounting;
- adapter access behavior.

### 5.2. Make checked casts identity-preserving

For a source-level checked cast:

1. evaluate the operand exactly once;
2. run the shared conformance check;
3. return the original value unchanged when it succeeds;
4. throw `InvalidConversion` at the cast span when it returns `false`.

Remove transforming helpers from the checked-cast path:

- string formatting;
- floating-point-to-integer conversion;
- integer-to-floating-point conversion.

These helpers may remain where required by implicit or contextual conversion execution.

### 5.3. Correct numeric type tests

Remove the special case that makes a runtime `long` satisfy a `float` type test.

Runtime numeric conformance must be:

- `long` for `int`;
- `double` for `float`;
- either representation for `number`.

### 5.4. Unify generic-object handling

Replace the shallow generic-object conversion check with the same conformance check used by `is object`.

Review cyclic and deeply nested object graphs. If runtime conformance is intended to support cycles, add pair or identity tracking appropriate to a single-value type traversal. Otherwise, document traversal-depth failure as an operational limitation shared by both operators.

## 6. IR validation and exporter changes

Update IR validation so that:

- implicit conversion instructions remain valid according to implicit conversion rules;
- checked conversion instructions are validated according to checked-cast eligibility;
- unchecked acquisition of mutable array capability remains invalid;
- type-test targets remain non-void;
- exporter behavior cannot reintroduce transforming checked casts.

Review the current `IrInstruction.Convert` representation. If its Boolean checked flag is insufficient to enforce the distinction, replace it with an explicit conversion mode.

The .NET exporter must route:

- implicit conversions through conversion execution;
- checked casts through shared conformance plus identity-preserving return.

## 7. Test plan

### 7.1. Type-relation tests

Add coverage proving that:

- implicit `int`-to-`float` conversion remains available;
- `int` is not eligible for `as float`;
- `number` remains eligible for checked casts to `int` and `float`;
- primitive and `null` sources are not eligible for `as string`;
- `unknown` remains eligible for checked casts to non-void target types;
- nullable, object, and array checked-cast eligibility remains available.

### 7.2. Binder tests

Add diagnostics for transforming source-level casts known to be invalid statically.

Verify successful binding for casts whose outcome depends on runtime representation or shape, including:

- `number`;
- `unknown` and `unknown?`;
- nullable values;
- structured objects;
- mutable and read-only arrays.

Verify that compiler-inserted implicit numeric conversions continue to bind and lower normally.

### 7.3. Exporter and runtime tests

Create paired `is`/`as` tests for every supported type family. For the same runtime value and target type:

- when `is` is `true`, `as` must return the original value;
- when `is` is `false`, `as` must throw `InvalidConversion`.

Cover:

- all primitive runtime representations;
- nullable and null values;
- `number` containing concrete `int` and `float` values;
- `unknown` containing each supported runtime representation;
- generic and structured objects;
- open and closed object shapes;
- mutable arrays;
- read-only arrays;
- nested arrays and objects;
- capability mismatches;
- element-shape mismatches.

Add identity assertions for successful object and array casts.

Add regressions proving that:

- `is float` no longer accepts an integer representation;
- checked casts do not format strings;
- checked casts do not perform numeric conversion;
- string concatenation still performs the specified contextual formatting;
- implicit numeric promotion still works in assignments, calls, returns, and operators.

### 7.4. IR tests

Add validation tests distinguishing implicit numeric conversion from source-level checked casting.

Verify that hand-authored or malformed IR cannot mark a transforming conversion as a valid checked cast.

## 8. Documentation and migration guidance

Update public examples only if they currently use transforming `as` expressions.

Migration guidance must state:

- use string concatenation when contextual primitive formatting is intended;
- use the planned standard-library numeric APIs when an actual numeric transformation is intended;
- replace an integer-to-floating-point `as` conversion with arithmetic promotion through `integer + 0.0` when the source has static type `int`;
- use `is` before `as` when cast failure must be avoided;
- do not rely on `as` for integer-to-floating-point promotion.

Avoid presenting `truncateToInt` as an exact replacement for the removed integral-value check. Its truncation semantics are intentionally different.

Do not present `number + 0.0` as a general replacement for `number as float`: although it produces a concrete floating-point result at runtime, an operand with static type `number` keeps the expression's static result type as `number`.

## 9. Changelog plan

For version `0.2.0-alpha.2`, add an incremental entry from `0.2.0-alpha.1` under `Breaking changes` in `CHANGELOG.detailed.md`. Do not add the prerelease entry to `CHANGELOG.md`.

The entry must describe:

- the new `is`/`as` equivalence guarantee;
- removal of primitive and `null` string conversion through `as`;
- removal of numeric representation changes through `as`;
- the fact that implicit numeric conversions remain available;
- migration through string concatenation, `integer + 0.0` for a source of static type `int`, and standard-library numeric functions;
- the runtime-representation behavior of casts from `number` and `unknown`.

Link to the most specific updated language-specification section after verifying the generated documentation anchor.

## 10. Validation

Run the smallest targeted test projects covering:

- core type relations;
- compiler binding and lowering;
- IR validation;
- .NET exporter runtime behavior.

Then run the repository's full Debug and Release validation because the change affects the language contract, compiler, IR, exporter, and public documentation.

The implementation is complete only when:

- every well-formed corresponding `is`/`as` pair satisfies the equivalence rule for semantic success and failure;
- successful checked casts preserve runtime representation and identity;
- implicit and contextual conversions retain their specified behavior;
- the specification contains no remaining statement that intentionally separates `is` success from `as` success;
- the breaking-change migration is ready for the next release changelog.
