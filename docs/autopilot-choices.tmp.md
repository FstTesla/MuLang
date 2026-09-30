# Autopilot choices

## Runtime error compatibility

- The existing `MuLangRuntimeException(string, string, TextSpan, Exception?)`
  constructor remains available. Errors created through that compatibility
  constructor default to catchable `Operation` errors because that matches the
  historical intrinsic-operation use of the constructor inside the exporter.
- `RuntimeError` is publicly immutable. The exporter can append MuLang frames
  internally while an exception unwinds, before the error is returned to the
  host.
- Runtime stack frames contain the portable function ID and the source span
  that entered the function. Frames are ordered from innermost to outermost,
  ending with `$entry`.

## Execution API

- `DotNetExportResult` keeps its existing positional constructor and
  `Delegate`. A computed get-only `ExecutionDelegate` is added instead of
  changing the positional record contract; computing it from the current
  `Delegate` also keeps record `with` expressions consistent.
- `ExecutionResult.Value` may be null for a successful execution. `IsSuccess`
  and `Error` disambiguate successful null results from failures.
- `ExecutionResult.HostException` preserves the underlying host failure that
  would be available through `MuLangRuntimeException.InnerException`.

## Provider invocation API

- The existing `DotNetFunction` delegate and `DotNetRuntimeContext`
  constructor remain available. A parallel canonical `DotNetProviderFunction`
  delegate is registered through `DotNetRuntimeContext.Create`, avoiding
  ambiguous constructor overloads for empty collection expressions.
- `DotNetProviderInvocationContext` is valid only for the synchronous provider
  invocation that receives it. Retained contexts fail with
  `InvalidOperationException` after the invocation completes.
- Object property names are copied while the runtime context is active and are
  charged to the execution budget. Providers do not receive an unmetered live
  property-name collection.
- `MuLangProviderException.Payload` is named `Payload` rather than `Data`
  because `Exception.Data` already defines an incompatible public property.
- Runtime exceptions created internally by exporter services carry an internal
  marker so they can propagate through provider code. A provider that directly
  constructs and throws `MuLangRuntimeException` is treated as an unexpected
  provider failure and cannot bypass the provider-error contract.
- Fatal host exceptions (`OutOfMemoryException`, `StackOverflowException`, and
  `AccessViolationException`) are not normalized into `Provider` errors.
