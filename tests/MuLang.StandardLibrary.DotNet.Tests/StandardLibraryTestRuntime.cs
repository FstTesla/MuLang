using MuLang.Core.Environment;
using MuLang.Core.Runtime;
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
        StandardLibraryModule module,
        string name,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default,
        DotNetStandardLibrary? standardLibrary = null
    )
    {
        StandardLibraryFunction function = module.Functions.Single(
            declaration => string.Equals(declaration.Name, name, StringComparison.Ordinal)
        );
        StandardLibrarySelection selection = StandardLibrarySelection.Create([ function ]);
        EnvironmentSchema environment = StandardLibraryComposer.Compose(selection);
        DotNetStandardLibraryBindings bindings = (standardLibrary ?? DotNetStandardLibrary.Default)
            .Bind(selection);
        DotNetRuntimeContext context = DotNetRuntimeContext.Create(
            environment,
            bindings.Globals,
            bindings.Functions,
            cancellationToken: cancellationToken
        );

        try
        {
            return InvokeMethod.Invoke(
                context,
                [ function.Id, arguments, function.Declaration.ReturnType, new TextSpan(0, 0) ]
            );
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    public static MuLangRuntimeException InvokeError(
        StandardLibraryModule module,
        string name,
        params object?[] arguments
    )
    {
        return Assert.Throws<MuLangRuntimeException>(
            () => Invoke(module, name, arguments)
        )!;
    }
}
