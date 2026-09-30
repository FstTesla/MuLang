# MuLang Examples

## Compile and execute an expression

The environment schema defines the names and types visible to MuLang source code. Runtime values are supplied separately and are keyed by their provider identifiers.

```csharp
using MuLang.Compiler;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;

EnvironmentSchema environment = new EnvironmentBuilder()
    .AddGlobal("global.value", "value", TypeSymbols.Int)
    .Build();

CompilationResult compilation = MuLangCompiler.Compile(
    "value + 1",
    environment,
    CompilationMode.Expression,
    TypeSymbols.Int
);

if (!compilation.IsSuccessful)
{
    throw new InvalidOperationException("MuLang compilation failed.");
}

DotNetExportResult export = DotNetExporter.Export(
    compilation.Program ??
        throw new InvalidOperationException("The compiled IR is unavailable."),
    environment
);
Func<DotNetRuntimeContext, ExecutionResult> execute = export.ExecutionDelegate ??
    throw new InvalidOperationException("The exported delegate is unavailable.");

DotNetRuntimeContext context = new (
    environment,
    [ new KeyValuePair<string, object?>("global.value", 41L) ],
    [ ]
);

ExecutionResult result = execute(context);
```

`result.IsSuccess` is `true`, and `result.Value` is a boxed `long` with value
`42`, matching the .NET runtime representation of the MuLang `int` type. The
exception-based `export.Delegate` remains available when the host prefers
exception propagation.
