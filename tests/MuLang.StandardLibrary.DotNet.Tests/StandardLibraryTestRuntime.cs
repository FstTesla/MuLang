using MuLang.Core.Environment;
using MuLang.Core.Runtime;
using MuLang.Core.Symbols;
using MuLang.Core.Text;
using MuLang.Exporters.DotNet;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace MuLang.StandardLibrary.DotNet.Tests;

internal static class StandardLibraryTestRuntime
{
    private static readonly MethodInfo InvokeMethod = typeof(DotNetRuntimeContext).GetMethod(
        "Invoke",
        BindingFlags.Instance | BindingFlags.NonPublic
    )!;

    public static object? Invoke(
        DotNetStandardLibraryModuleBinding binding,
        string name,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default
    )
    {
        EnvironmentSchema environment = StandardLibraryComposer.Compose([ binding.Module ]);
        DotNetStandardLibraryComposition composition = DotNetStandardLibraryComposer.Compose([ binding ]);
        DotNetRuntimeContext context = DotNetRuntimeContext.Create(
            environment,
            composition.Globals,
            composition.Functions,
            cancellationToken: cancellationToken
        );
        FunctionSymbol function = binding.Module.Functions.Single(
            declaration => string.Equals(declaration.Name, name, StringComparison.Ordinal)
        );

        try
        {
            return InvokeMethod.Invoke(
                context,
                [ function.Id, arguments, function.ReturnType, new TextSpan(0, 0) ]
            );
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    public static MuLangRuntimeException InvokeError(
        DotNetStandardLibraryModuleBinding binding,
        string name,
        params object?[] arguments
    )
    {
        return Assert.Throws<MuLangRuntimeException>(
            () => Invoke(binding, name, arguments)
        )!;
    }
}
