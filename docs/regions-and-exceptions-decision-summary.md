# Regions and exceptions decision summary

1. **Question:** How should lifetime regions and exception regions relate in the IR model?

   **Answer:** They use distinct, coordinated hierarchies.

2. **Question:** Where is basic-block membership represented?

   **Answer:** Each block stores one lifetime-region owner; exception regions list their blocks.

3. **Question:** When is a nested lifetime region created?

   **Answer:** For every lexical scope that owns at least one local slot.

4. **Question:** Which MuIR version introduces lifetime regions, scoped slots, and exception regions?

   **Answer:** MuIR version 2.

5. **Question:** Must prerelease MuIR 2 documents produced before regions remain readable?

   **Answer:** No. The previous prerelease MuIR 2 grammar is intentionally incompatible.

6. **Question:** What is the public lifetime-region type called?

   **Answer:** `IrLifetimeRegion`.

7. **Question:** What is the public shape of an exception region?

   **Answer:** One `IrExceptionRegion` record with optional handler and cleanup components.

8. **Question:** Is the function root lifetime region explicit?

   **Answer:** Yes. It is serialized with ID `0`.

9. **Question:** How are region IDs allocated?

   **Answer:** Lifetime and exception regions use separate contiguous, zero-based namespaces within each function.

10. **Question:** How is `IrSlot` API compatibility preserved?

    **Answer:** Existing constructors remain and assign lifetime root region `0`.

11. **Question:** How is `IrBasicBlock` API compatibility preserved?

    **Answer:** Existing constructors remain and assign lifetime root region `0`.

12. **Question:** How are protected, handler, and cleanup block memberships represented?

    **Answer:** Through explicit block-ID collections and explicit entry blocks.

13. **Question:** Is the exception-region parent explicit?

    **Answer:** Yes.

14. **Question:** Must lifetime-region and exception-region boundaries align?

    **Answer:** No. Their boundaries are independent and cross-validated.

15. **Question:** What creates a new lifetime-region activation?

    **Answer:** Entry through the region entry from its parent; re-entry must first pass through the parent.

16. **Question:** Are scoped-slot storage locations cleared on region entry?

    **Answer:** No.

17. **Question:** How are read-only scoped slots validated?

    **Answer:** With path-sensitive dataflow per region activation.

18. **Question:** How are pending completions represented in MuIR?

    **Answer:** `Jump`, `Branch`, `Return`, and `Throw` create pending completions contextually when their selected transfer crosses exception-region boundaries.

19. **Question:** Are separate `Leave` and `LeaveReturn` terminators required?

    **Answer:** No. Their behavior is unified into `Jump` and `Return`.

20. **Question:** How is the current handler error stored?

    **Answer:** Every handler has one mandatory read-only error slot.

21. **Question:** How many handlers may an exception region contain?

    **Answer:** Zero or one.

22. **Question:** Which overlaps are permitted between exception regions?

    **Answer:** Regions must be disjoint or strictly nested; partial overlap is invalid.

23. **Question:** Does the protected component declare an explicit entry?

    **Answer:** Yes.

24. **Question:** May a child exception region cross multiple parts of its parent?

    **Answer:** No. It is contained entirely in one parent part.

25. **Question:** How is the cleanup chain determined?

    **Answer:** It is derived from the exception-region hierarchy.

26. **Question:** How are exception-region boundary exits identified?

    **Answer:** They are derived automatically from the source block, selected target, and exception-region hierarchy.

27. **Question:** Which lifetime region owns compiler-generated temporaries?

    **Answer:** The active region or the nearest ancestor required by all uses.

28. **Question:** How does `IrBuilder` construct regions?

    **Answer:** With separate structured stacks for lifetime and exception regions.

29. **Question:** How is `IrFunction` API compatibility preserved?

    **Answer:** Existing constructors synthesize lifetime root region `0` and no exception regions.

30. **Question:** Does MuIR 2 always serialize the lifetime root and region counts?

    **Answer:** Yes.

31. **Question:** How are reader limits defined for regions?

    **Answer:** Lifetime and exception regions have separate configurable limits.

32. **Question:** How complete is exception-region support before source syntax is added?

    **Answer:** It is end-to-end across the IR model, validator, MuIR, runtime, and .NET exporter.

33. **Question:** How is the executable error type named without conflicting with the existing compiler sentinel?

    **Answer:** The recovery sentinel is canonically `TypeSymbols.ErrorRecovery` and `TypeKind.ErrorRecovery`, with obsolete `Error` aliases; the executable type is `TypeSymbols.ErrorValue` and `TypeKind.ErrorValue`.

34. **Question:** Does point 6 introduce an explicit throw terminator?

    **Answer:** Yes. `IrTerminator.Throw` consumes an `ErrorValue` slot containing an `IDotNetErrorValue`.

35. **Question:** Which type relations apply to `TypeSymbols.ErrorValue`?

    **Answer:** It is an intrinsic non-null type assignable to `object`, `unknown`, and any structurally compatible object type through a read-only identity-preserving view; the reverse conversion is not implicit.

36. **Question:** Does every handler have a dedicated lifetime region?

    **Answer:** Yes.

37. **Question:** How are completions displaced by a cleanup error exposed?

    **Answer:** They remain internal and are not exposed through a new public host API.

38. **Question:** How is a `RuntimeError` represented as a MuLang runtime value?

    **Answer:** Through a wrapper that implements the required .NET object interfaces.

39. **Question:** Which interfaces does the public error-value contract extend?

    **Answer:** `IDotNetObjectValue` and `IDotNetObjectPropertyCapabilities`.

40. **Question:** What is the public error-value interface called?

    **Answer:** `IDotNetErrorValue`.

41. **Question:** Does `IDotNetErrorValue` expose the underlying structured error?

    **Answer:** Yes, through `RuntimeError Error`.

42. **Question:** Must one `RuntimeError` always have one host wrapper?

    **Answer:** No. Multiple wrappers are allowed; MuLang identity uses the underlying `RuntimeError`.

43. **Question:** What is the source-facing type of `error.category`?

    **Answer:** `string`.

44. **Question:** How is the source span exposed on `error`?

    **Answer:** Through read-only `spanStart` and `spanLength` integer properties.

45. **Question:** Are absent `data` and present `data` with value `null` distinct?

    **Answer:** Yes.

46. **Question:** May providers implement `IDotNetErrorValue` themselves?

    **Answer:** Yes, if the implementation satisfies the complete contract.

47. **Question:** Which release targets the coordinated lifetime-region and exception-region work?

    **Answer:** `0.3.0-alpha.3`.

48. **Question:** Do exception-region block collections include blocks owned by child exception regions?

    **Answer:** No. They contain direct members only.

49. **Question:** How does a child exception region identify its location in the parent?

    **Answer:** Through an explicit `ParentPart` value of `Protected`, `Handler`, or `Cleanup`.

50. **Question:** Does the reader continue supporting MuIR version 1?

    **Answer:** Yes, by synthesizing lifetime root region `0` and no exception regions.

51. **Question:** When is `TypeSymbols.ErrorValue` available?

    **Answer:** In MuLang 1.2, independently from the source exception-handling feature flag.

52. **Question:** Is `error` a reserved keyword?

    **Answer:** Yes. MuLang 1.2 reserves it and earlier versions produce a migration warning.

53. **Question:** Which object properties does a MuLang error expose?

    **Answer:** `code`, `category`, `message`, `cause`, `data`, `spanStart`, and `spanLength`, all read-only.

54. **Question:** Which definite-assignment state enters handlers and cleanup?

    **Answer:** The conservative intersection of every path that can reach the component.

55. **Question:** Which IR instructions are considered capable of raising a catchable error for dataflow?

    **Answer:** Every instruction.

56. **Question:** How does a cleanup complete normally?

    **Answer:** With the dedicated `Resume` terminator.

57. **Question:** Which new terminators are introduced?

    **Answer:** Only `Throw` and `Resume`; `Jump`, `Branch`, and `Return` become context-sensitive.

58. **Question:** May `Branch` cross exception-region boundaries without bridge blocks?

    **Answer:** Yes. The selected edge independently derives its cleanup chain after evaluating the condition.

59. **Question:** Does block order in region membership collections carry meaning?

    **Answer:** No. Collections have set semantics and serialize in ascending block-ID order.

60. **Question:** How are optional handler and cleanup data represented inside `IrExceptionRegion`?

    **Answer:** With `IrExceptionHandler` and `IrExceptionCleanup` component records.

61. **Question:** Is the protected part also a component record?

    **Answer:** Yes, through `IrExceptionProtectedRegion`.

62. **Question:** How are provider implementations of `IDotNetErrorValue` handled at the runtime boundary?

    **Answer:** Their full object contract is validated and the provider wrapper is preserved.

63. **Question:** What are the equality semantics of error values?

    **Answer:** Structural equality compares the seven object properties; identity equality compares the underlying `RuntimeError`.

64. **Question:** May provider error wrappers expose additional object properties?

    **Answer:** No.

65. **Question:** Which canonical strings represent runtime error categories?

    **Answer:** Lower camel case strings, including `runtimeContract`.

66. **Question:** How is optional runtime-error data represented in the public API?

    **Answer:** Through a dedicated `RuntimeErrorData` value type and compatible overloads.

67. **Question:** In which order is the coordinated implementation performed?

    **Answer:** Complete lifetime regions first, then implement exception regions end-to-end on the same MuIR 2 contract.

68. **Question:** How many direct exception-region owners may a basic block have?

    **Answer:** Zero or one.

69. **Question:** When does `Return` capture its value if cleanup is required?

    **Answer:** Before lifetime-region deactivation and before cleanup begins.
