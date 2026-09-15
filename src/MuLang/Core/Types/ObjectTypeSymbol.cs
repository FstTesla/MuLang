using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.Core.Types;

/// <summary>Represents a structured MuLang object type.</summary>
public sealed class ObjectTypeSymbol : TypeSymbol
{
    private readonly IReadOnlyDictionary<string, ObjectPropertySymbol> propertiesByName;

    /// <summary>Initializes a new instance of the <see cref="ObjectTypeSymbol" /> class.</summary>
    /// <param name="id">The provider identifier.</param>
    /// <param name="name">The language name.</param>
    /// <param name="isOpen">A value indicating whether undeclared properties are permitted.</param>
    /// <param name="properties">The declared object properties.</param>
    /// <exception cref="ArgumentException">Thrown when the identifier or name is invalid, or when a property name is duplicated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="properties" /> is <c>null</c>.</exception>
    public ObjectTypeSymbol(
        string id,
        string name,
        bool isOpen,
        IEnumerable<ObjectPropertySymbol> properties
    )
        : this(id, name, isOpen, properties, true) { }

    private ObjectTypeSymbol(
        string? id,
        string name,
        bool isOpen,
        IEnumerable<ObjectPropertySymbol> properties,
        bool validateProviderIdentity
    )
        : base(TypeKind.StructuredObject)
    {
        if (validateProviderIdentity && string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Type identifier cannot be null or whitespace.", nameof(id));
        }

        if (validateProviderIdentity)
        {
            LanguageNames.ValidateIdentifier(name, nameof(name));
        }

        if (properties is null)
        {
            throw new ArgumentNullException(nameof(properties));
        }

        IReadOnlyCollection<ObjectPropertySymbol> propertyList = [ .. properties ];

        IGrouping<string, ObjectPropertySymbol>? duplicate = propertyList
            .GroupBy(static property => property.Name, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Property '{duplicate.Key}' is declared more than once.",
                nameof(properties)
            );
        }

        Id = id;
        Name = name;
        IsOpen = isOpen;
        Properties = propertyList;
        propertiesByName = propertyList.ToFrozenDictionary(
            static property => property.Name,
            StringComparer.Ordinal
        );
    }

    /// <summary>Gets the provider identifier, or <c>null</c> for an anonymous type.</summary>
    public string? Id { get; }

    /// <summary>Gets the language name.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether undeclared properties are permitted.</summary>
    public bool IsOpen { get; }

    /// <summary>Gets the declared properties.</summary>
    public IReadOnlyCollection<ObjectPropertySymbol> Properties { get; }

    /// <inheritdoc />
    public override string DisplayName => Name;

    /// <summary>Gets a property by name.</summary>
    /// <param name="name">The language name.</param>
    /// <param name="property">When this method returns, contains the property, if found.</param>
    /// <returns><c>true</c> if a property with the specified name was found; otherwise, <c>false</c>.</returns>
    public bool TryGetProperty(
        string name,
        [NotNullWhen(true)] out ObjectPropertySymbol? property
    )
    {
        return propertiesByName.TryGetValue(name, out property);
    }

    internal static ObjectTypeSymbol CreateAnonymous(
        bool isOpen,
        IEnumerable<ObjectPropertySymbol> properties
    )
    {
        return new ObjectTypeSymbol(null, "<anonymous>", isOpen, properties, false);
    }
}
