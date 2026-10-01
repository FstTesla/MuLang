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

DotNetRuntimeContext context = DotNetRuntimeContext.Create(
    environment,
    [ new KeyValuePair<string, object?>("global.value", 41L) ],
    [ ]
);

ExecutionResult result = execute(context);
```

`result.IsSuccess` is `true`, and `result.Value` is a boxed `long` with value
`42`, matching the .NET runtime representation of the MuLang `int` type. The
exception-based `export.Delegate` remains available as a compatibility view
derived from `ExecutionDelegate` when the host prefers exception propagation.

## Select standard-library symbols

Standard-library symbols and proposed modules are selected explicitly. The same
normalized selection drives environment composition and .NET binding.

```csharp
using MuLang.StandardLibrary;
using MuLang.StandardLibrary.DotNet;

StandardLibrarySelection mathSelection = StandardLibrarySelection.Create(
    [
        StandardLibraryCatalog.Globals.Pi,
        StandardLibraryCatalog.Functions.Abs,
        StandardLibraryCatalog.Functions.Round,
    ]
);
EnvironmentSchema mathEnvironment =
    StandardLibraryComposer.Compose(mathSelection);
DotNetStandardLibraryBindings mathRuntime =
    DotNetStandardLibrary.Default.Bind(mathSelection);
```

The selection can also combine complete modules with provider-defined globals
and functions.

```csharp
using MuLang.Compiler;
using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Symbols;
using MuLang.Core.Types;
using MuLang.Exporters.DotNet;
using MuLang.StandardLibrary;
using MuLang.StandardLibrary.DotNet;

EnvironmentSchema hostEnvironment = new EnvironmentBuilder()
    .AddGlobal("host.orderTotal", "orderTotal", TypeSymbols.Float)
    .AddGlobal("host.discountRate", "discountRate", TypeSymbols.Float)
    .AddGlobal("host.taxRate", "taxRate", TypeSymbols.Float)
    .AddGlobal("host.customerName", "customerName", TypeSymbols.String)
    .AddFunction(
        "host.applyDiscount",
        "applyDiscount",
        [
            new ParameterSymbol("amount", TypeSymbols.Float),
            new ParameterSymbol("rate", TypeSymbols.Float),
        ],
        TypeSymbols.Float
    )
    .Build();

StandardLibrarySelection selection = StandardLibrarySelection.CreateFromModules(
    [
        StandardLibraryCatalog.Modules.MathBasic,
        StandardLibraryCatalog.Modules.MathRounding,
        StandardLibraryCatalog.Modules.StringTransform,
    ]
);
EnvironmentSchema environment =
    StandardLibraryComposer.Compose(hostEnvironment, selection);

DotNetProviderFunction applyDiscount = static (context, arguments) =>
{
    context.ThrowIfCancellationRequested();
    double amount = (double)arguments[0]!;
    double rate = (double)arguments[1]!;
    return amount * (1d - rate);
};

DotNetStandardLibraryBindings standardBindings =
    DotNetStandardLibrary.Default.Bind(selection);
IEnumerable<KeyValuePair<string, object?>> globals =
    standardBindings.Globals.Concat(
        [
            KeyValuePair.Create<string, object?>("host.orderTotal", 100d),
            KeyValuePair.Create<string, object?>("host.discountRate", 0.2d),
            KeyValuePair.Create<string, object?>("host.taxRate", 0.075d),
            KeyValuePair.Create<string, object?>("host.customerName", " Ada "),
        ]
    );
IEnumerable<KeyValuePair<string, DotNetProviderFunction>> functions =
    standardBindings.Functions.Concat(
        [ KeyValuePair.Create("host.applyDiscount", applyDiscount) ]
    );

const string source = """
    var discounted = applyDiscount(orderTotal, discountRate);
    var taxed = discounted * (1.0 + taxRate);
    var total = round(max(taxed, 0.0));
    return toUpper(trim(customerName)) + ": " + total;
    """;

CompilationResult compilation = MuLangCompiler.Compile(
    source,
    environment,
    CompilationMode.Program,
    TypeSymbols.String
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

DotNetRuntimeContext context = DotNetRuntimeContext.Create(
    environment,
    globals,
    functions
);
ExecutionResult result = execute(context);
```

The result is the string `"ADA: 86.0"`. The declaration composer rejects name or
provider-identifier collisions with the host environment. The runtime context
rejects duplicate runtime identifiers when standard and host bindings are
combined.
