namespace MuLang.Core.Types;

/// <summary>Represents a property declared by a structured object type.</summary>
public sealed class ObjectPropertySymbol
{
    /// <summary>Initializes a new instance of the <see cref="ObjectPropertySymbol" /> class.</summary>
    /// <param name="name">The language name.</param>
    /// <param name="type">The type.</param>
    /// <param name="isOptional">A value indicating whether the property may be omitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name" /> or <paramref name="type" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="type" /> cannot be used for an object property.</exception>
    public ObjectPropertySymbol(string name, TypeSymbol type, bool isOptional = false)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (type.Kind is TypeKind.Void or TypeKind.Null or TypeKind.Error)
        {
            throw new ArgumentException(
                $"Type '{type.DisplayName}' cannot be used for an object property.",
                nameof(type)
            );
        }

        Name = name;
        Type = type;
        IsOptional = isOptional;
    }

    /// <summary>Gets the property name.</summary>
    public string Name { get; }

    /// <summary>Gets the property type.</summary>
    public TypeSymbol Type { get; }

    /// <summary>Gets a value indicating whether the property may be omitted.</summary>
    public bool IsOptional { get; }
}
