using MuLang.Core.Runtime;

namespace MuLang.Exporters.DotNet;

/// <summary>Represents an interface for an executable MuLang error value.</summary>
public interface IDotNetErrorValue
    : IDotNetObjectValue,
        IDotNetObjectPropertyCapabilities
{
    /// <summary>Gets the underlying structured runtime error.</summary>
    RuntimeError Error { get; }
}
