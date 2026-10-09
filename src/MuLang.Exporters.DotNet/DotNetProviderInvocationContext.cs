using MuLang.Core.Runtime;
using MuLang.Core.Text;
using MuLang.Core.Types;
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

    /// <summary>Determines whether two runtime values have the same MuLang identity.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><c>true</c> if the values have the same identity; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool IdentityEquals(object? left, object? right)
    {
        EnsureActive();
        runtimeContext.Consume(span);
        return DotNetRuntimeOperations.IdentityEquals(
            runtimeContext,
            left,
            right,
            span
        );
    }

    /// <summary>Determines whether a runtime value is represented as an object.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><c>true</c> if the value is an object; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool IsObjectValue(object? value)
    {
        EnsureActive();
        runtimeContext.Consume(span);
        return value is IDotNetObjectValue;
    }

    /// <summary>Determines whether a runtime value is represented as an array.</summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><c>true</c> if the value is an array; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool IsArrayValue(object? value)
    {
        EnsureActive();
        runtimeContext.Consume(span);
        return value is IDotNetReadOnlyArrayValue or IDotNetArrayValue;
    }

    /// <summary>Creates an array value and validates its elements against the specified type.</summary>
    /// <param name="type">The array type, including its element and mutability capabilities.</param>
    /// <param name="elements">The array elements.</param>
    /// <returns>The new array value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when an element is incompatible with <paramref name="type" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public object CreateArray(
        ArrayTypeSymbol type,
        IEnumerable<object?> elements
    )
    {
        EnsureActive();

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (elements is null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        List<object?> values = [ ];

        foreach (object? element in elements)
        {
            runtimeContext.Consume(span);
            values.Add(element);
        }

        object array = DotNetRuntimeOperations.CreateArray(
            values,
            type.IsReadOnly
        );

        if (!DotNetRuntimeOperations.IsValueOfTypeDeep(
                runtimeContext,
                array,
                type,
                span
            ))
        {
            throw new ArgumentException(
                $"An array element is incompatible with '{type.DisplayName}'.",
                nameof(elements)
            );
        }

        return array;
    }

    /// <summary>Creates an object value and validates it against the specified type.</summary>
    /// <param name="type">The structured object type.</param>
    /// <param name="properties">The present property names and values.</param>
    /// <returns>The new object value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when properties are duplicated or incompatible with <paramref name="type" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public object CreateObject(
        ObjectTypeSymbol type,
        IEnumerable<KeyValuePair<string, object?>> properties
    )
    {
        EnsureActive();

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (properties is null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        List<KeyValuePair<string, object?>> values = [ ];
        ISet<string> names = new HashSet<string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, object?> property in properties)
        {
            runtimeContext.Consume(span);

            if (property.Key is null)
            {
                throw new ArgumentException(
                    "Object property names cannot be null.",
                    nameof(properties)
                );
            }

            if (!names.Add(property.Key))
            {
                throw new ArgumentException(
                    $"Object property '{property.Key}' is specified more than once.",
                    nameof(properties)
                );
            }

            values.Add(property);
        }

        object value = DotNetRuntimeOperations.CreateObject(type, values);

        if (!DotNetRuntimeOperations.IsValueOfTypeDeep(
                runtimeContext,
                value,
                type,
                span
            ))
        {
            throw new ArgumentException(
                $"The properties do not conform to '{type.DisplayName}'.",
                nameof(properties)
            );
        }

        return value;
    }

    /// <summary>Creates an error value from a runtime error.</summary>
    /// <param name="error">The runtime error.</param>
    /// <returns>The corresponding MuLang error value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public object CreateErrorValue(RuntimeError error)
    {
        EnsureActive();

        if (error is null)
        {
            throw new ArgumentNullException(nameof(error));
        }

        runtimeContext.Consume(span);
        return DotNetRuntimeOperations.WrapError(error);
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

    /// <summary>Sets an element in a mutable array value.</summary>
    /// <param name="value">The array value.</param>
    /// <param name="index">The zero-based element index.</param>
    /// <param name="element">The new element value.</param>
    /// <returns><c>true</c> if the element was set; otherwise, <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool TrySetArrayElement(
        object value,
        int index,
        object? element
    )
    {
        EnsureActive();
        runtimeContext.Consume(span);
        return value is IDotNetArrayValue array &&
            array.TrySetElement(index, element);
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

    /// <summary>Determines whether an object adapter exposes a property's read-only capability.</summary>
    /// <param name="value">The object value.</param>
    /// <param name="name">The property name.</param>
    /// <param name="isReadOnly">When this method returns, contains the property's capability, if available.</param>
    /// <returns><c>true</c> if the adapter exposes the capability; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool TryGetObjectPropertyReadOnly(
        object value,
        string name,
        out bool isReadOnly
    )
    {
        EnsureActive();

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        runtimeContext.Consume(span);

        if (
            value is IDotNetObjectValue &&
            value is IDotNetObjectPropertyCapabilities capabilities
        )
        {
            isReadOnly = capabilities.IsPropertyReadOnly(name);
            return true;
        }

        isReadOnly = false;
        return false;
    }

    /// <summary>Sets a property on an object value.</summary>
    /// <param name="value">The object value.</param>
    /// <param name="name">The property name.</param>
    /// <param name="propertyValue">The new property value.</param>
    /// <returns><c>true</c> if the property was set; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool TrySetObjectProperty(
        object value,
        string name,
        object? propertyValue
    )
    {
        EnsureActive();

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        runtimeContext.Consume(span);
        return value is IDotNetObjectValue objectValue &&
            objectValue.TrySetProperty(name, propertyValue);
    }

    /// <summary>Removes a property from an object value.</summary>
    /// <param name="value">The object value.</param>
    /// <param name="name">The property name.</param>
    /// <returns><c>true</c> if the property was removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    public bool TryRemoveObjectProperty(
        object value,
        string name
    )
    {
        EnsureActive();

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        runtimeContext.Consume(span);
        return value is IDotNetObjectValue objectValue &&
            objectValue.TryRemoveProperty(name);
    }

    /// <summary>Reports an expected application failure.</summary>
    /// <param name="code">The stable application error code.</param>
    /// <param name="message">The application error message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    /// <exception cref="MuLangProviderException">Always thrown for an active invocation.</exception>
    [DoesNotReturn]
    public void ThrowApplicationError(
        string code,
        string message
    )
    {
        ThrowApplicationError(
            code,
            message,
            null,
            RuntimeErrorData.Absent
        );
    }

    /// <summary>Reports an expected application failure.</summary>
    /// <param name="code">The stable application error code.</param>
    /// <param name="message">The application error message.</param>
    /// <param name="cause">The optional public MuLang error cause.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    /// <exception cref="MuLangProviderException">Always thrown for an active invocation.</exception>
    [DoesNotReturn]
    public void ThrowApplicationError(
        string code,
        string message,
        RuntimeError? cause
    )
    {
        ThrowApplicationError(
            code,
            message,
            cause,
            RuntimeErrorData.Absent
        );
    }

    /// <summary>Reports an expected application failure.</summary>
    /// <param name="code">The stable application error code.</param>
    /// <param name="message">The application error message.</param>
    /// <param name="cause">The optional public MuLang error cause.</param>
    /// <param name="data">The optional application payload.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    /// <exception cref="MuLangProviderException">Always thrown for an active invocation.</exception>
#pragma warning disable RS0027
    [DoesNotReturn]
    [Obsolete("Use ThrowApplicationError(string, string, RuntimeError?, RuntimeErrorData) instead.")]
    public void ThrowApplicationError(
        string code,
        string message,
        RuntimeError? cause = null,
        object? data = null
    )
    {
        ThrowApplicationError(
            code,
            message,
            cause,
            data is null
                ? RuntimeErrorData.Absent
                : RuntimeErrorData.Present(data)
        );
    }
#pragma warning restore RS0027

    /// <summary>Reports an expected application failure with an explicit optional payload.</summary>
    /// <param name="code">The stable application error code.</param>
    /// <param name="message">The application error message.</param>
    /// <param name="cause">The optional public MuLang error cause.</param>
    /// <param name="errorData">The optional application payload.</param>
    /// <exception cref="InvalidOperationException">Thrown when the invocation has completed.</exception>
    /// <exception cref="MuLangProviderException">Always thrown for an active invocation.</exception>
    [DoesNotReturn]
    public void ThrowApplicationError(
        string code,
        string message,
        RuntimeError? cause,
        RuntimeErrorData errorData
    )
    {
        EnsureActive();
        throw new MuLangProviderException(code, message, cause, errorData);
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
