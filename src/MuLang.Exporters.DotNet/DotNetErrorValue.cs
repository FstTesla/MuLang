using MuLang.Core.Runtime;

namespace MuLang.Exporters.DotNet;

internal sealed class DotNetErrorValue : IDotNetErrorValue
{
    private static readonly IReadOnlyCollection<string> requiredPropertyNames =
    [
        "code",
        "category",
        "message",
        "spanStart",
        "spanLength",
    ];

    public DotNetErrorValue(RuntimeError error)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public RuntimeError Error { get; }

    public object Identity => Error;

    public IReadOnlyCollection<string> PropertyNames
    {
        get
        {
            List<string> names = [ .. requiredPropertyNames ];

            if (Error.Cause is not null)
            {
                names.Add("cause");
            }

            if (Error.ErrorData.IsPresent)
            {
                names.Add("data");
            }

            return names.AsReadOnly();
        }
    }

    public bool TryGetProperty(string name, out object? value)
    {
        switch (name)
        {
            case "code":
            {
                value = Error.Code;
                return true;
            }

            case "category":
            {
                value = GetCategoryName(Error.Category);
                return true;
            }

            case "message":
            {
                value = Error.Message;
                return true;
            }

            case "cause" when Error.Cause is not null:
            {
                value = new DotNetErrorValue(Error.Cause);
                return true;
            }

            case "data" when Error.ErrorData.IsPresent:
            {
                value = Error.ErrorData.Value;
                return true;
            }

            case "spanStart":
            {
                value = (long)Error.Span.Start;
                return true;
            }

            case "spanLength":
            {
                value = (long)Error.Span.Length;
                return true;
            }

            default:
            {
                value = null;
                return false;
            }
        }
    }

    public bool TrySetProperty(string name, object? value)
    {
        return false;
    }

    public bool TryRemoveProperty(string name)
    {
        return false;
    }

    public bool IsPropertyReadOnly(string name)
    {
        return PropertyNames.Contains(name, StringComparer.Ordinal);
    }

    private static string GetCategoryName(RuntimeErrorCategory category)
    {
        return category switch
        {
            RuntimeErrorCategory.Operation => "operation",
            RuntimeErrorCategory.Mutation => "mutation",
            RuntimeErrorCategory.Application => "application",
            RuntimeErrorCategory.Provider => "provider",
            RuntimeErrorCategory.Resource => "resource",
            RuntimeErrorCategory.Cancellation => "cancellation",
            RuntimeErrorCategory.Environment => "environment",
            RuntimeErrorCategory.RuntimeContract => "runtimeContract",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }
}
