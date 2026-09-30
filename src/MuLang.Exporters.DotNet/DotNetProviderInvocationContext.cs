using MuLang.Core.Runtime;
using MuLang.Core.Text;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.Exporters.DotNet;

/// <summary>Provides runtime services to a .NET provider function invocation.</summary>
public sealed class DotNetProviderInvocationContext
{
    private readonly DotNetRuntimeContext runtimeContext;
    private readonly TextSpan span;
    private bool isActive = true;

    internal DotNetProviderInvocationContext(
        DotNetRuntimeContext runtimeContext,
        TextSpan span
    )
    {
        this.runtimeContext = runtimeContext;
        this.span = span;
    }

    /// <summary>Gets the execution cancellation token.</summary>
    public CancellationToken CancellationToken
    {
        get
        {
            EnsureActive();
            return runtimeContext.CancellationToken;
        }
    }

    /// <summary>Throws when execution cancellation has been requested.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    /// <exception cref="OperationCanceledException">Thrown when execution cancellation has been requested.</exception>
    public void ThrowIfCancellationRequested()
    {
        EnsureActive();
        runtimeContext.CancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Determines whether two runtime values are structurally equal.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><c>true</c> if the values are structurally equal; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool StructuralEquals(object? left, object? right)
    {
        EnsureActive();
        return DotNetRuntimeOperations.StructuralEquals(
            runtimeContext,
            left,
            right,
            span
        );
    }

    /// <summary>Gets the number of elements in a read-only array value.</summary>
    /// <param name="value">The array value.</param>
    /// <returns>The number of elements.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is not a read-only array value.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public int GetReadOnlyArrayCount(object value)
    {
        EnsureActive();
        runtimeContext.Consume(span);

        if (!DotNetRuntimeOperations.TryGetArrayCount(value, out int count))
        {
            throw new ArgumentException(
                "Value must be a .NET read-only array adapter.",
                nameof(value)
            );
        }

        return count;
    }

    /// <summary>Gets an element from a read-only array value.</summary>
    /// <param name="value">The array value.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="element">When this method returns, contains the element value, if found.</param>
    /// <returns><c>true</c> if the element was retrieved; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is not a read-only array value.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool TryGetReadOnlyArrayElement(
        object value,
        int index,
        out object? element
    )
    {
        EnsureActive();
        runtimeContext.Consume(span);

        if (
            value is not IDotNetReadOnlyArrayValue &&
            value is not IDotNetArrayValue
        )
        {
            throw new ArgumentException(
                "Value must be a .NET read-only array adapter.",
                nameof(value)
            );
        }

        return DotNetRuntimeOperations.TryGetArrayElement(
            value,
            index,
            out element
        );
    }

    /// <summary>Enumerates the available property names from an object value.</summary>
    /// <param name="value">The object value.</param>
    /// <returns>A controlled enumeration of the available property names.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is not a .NET object adapter.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public IEnumerable<string> GetObjectPropertyNames(object value)
    {
        EnsureActive();
        runtimeContext.Consume(span);

        if (!DotNetRuntimeOperations.TryGetPropertyNames(
                value,
                out IReadOnlyCollection<string>? names
            ))
        {
            throw new ArgumentException(
                "Value must be a .NET object adapter.",
                nameof(value)
            );
        }

        return new DotNetProviderPropertyNameEnumerable(this, names);
    }

    internal void EnsureEnumerationActive()
    {
        EnsureActive();
    }

    internal void ConsumeEnumerationStep()
    {
        EnsureActive();
        runtimeContext.Consume(span);
    }

    /// <summary>Gets a property from an object value.</summary>
    /// <param name="value">The object value.</param>
    /// <param name="name">The property name.</param>
    /// <param name="propertyValue">When this method returns, contains the property value, if found.</param>
    /// <returns><c>true</c> if the property was retrieved; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value" /> is not a .NET object adapter.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool TryGetObjectProperty(
        object value,
        string name,
        out object? propertyValue
    )
    {
        EnsureActive();

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        runtimeContext.Consume(span);

        if (value is not IDotNetObjectValue)
        {
            throw new ArgumentException(
                "Value must be a .NET object adapter.",
                nameof(value)
            );
        }

        return DotNetRuntimeOperations.TryGetProperty(
            value,
            name,
            out propertyValue
        );
    }

    /// <summary>Reports an expected application failure.</summary>
    /// <param name="code">The stable application error code.</param>
    /// <param name="message">The application error message.</param>
    /// <param name="cause">The optional public MuLang error cause.</param>
    /// <param name="data">The optional application payload.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    /// <exception cref="MuLangProviderException">Always thrown for an active invocation.</exception>
    [DoesNotReturn]
    public void ThrowApplicationError(
        string code,
        string message,
        RuntimeError? cause = null,
        object? data = null
    )
    {
        EnsureActive();
        throw new MuLangProviderException(code, message, cause, data);
    }

    internal void Complete()
    {
        isActive = false;
    }

    private void EnsureActive()
    {
        if (!isActive)
        {
            throw new InvalidOperationException(
                "The provider invocation has already completed."
            );
        }
    }
}
