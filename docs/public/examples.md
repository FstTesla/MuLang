# MuLang Examples

## Compile and execute an expression

The environment schema defines the names and types visible to MuLang source code. Runtime values are supplied separately and are keyed by their provider identifiers.

```csharp
using MuLang;
using MuLang.Core.Environment;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;

EnvironmentSchema environment = new EnvironmentBuilder()
    .AddGlobal("global.value", "value", TypeSymbols.Int)
    .Build();

CompilationResult compilation = MuLangCompiler.CompileExpression(
    "value + 1",
    environment,
    TypeSymbols.Int
);

if (!compilation.IsSuccessful)
{
    throw new InvalidOperationException("MuLang compilation failed.");
}

Func<DotNetRuntimeContext, object?> compiled = compilation.Delegate ??
    throw new InvalidOperationException("The compiled delegate is unavailable.");

DotNetRuntimeContext context = new (
    environment,
    [ new KeyValuePair<string, object?>("global.value", 41L) ],
    [ ]
);

object? result = compiled(context);
```

The result is a boxed `long` with value `42`, matching the .NET runtime representation of the MuLang `int` type.
