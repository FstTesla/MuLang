using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.Core.Types;

/// <summary>Represents a structured MuLang object type.</summary>
public sealed class ObjectTypeSymbol : TypeSymbol
{
    private IReadOnlyDictionary<string, ObjectPropertySymbol> propertiesByName;
    private bool isComplete;
    private IReadOnlyCollection<ObjectPropertySymbol> properties;

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
        : this(id, name, isOpen, true)
    {
        Complete(properties);
    }

    private ObjectTypeSymbol(
        string? id,
        string name,
        bool isOpen,
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

        Id = id;
        Name = name;
        IsOpen = isOpen;
        properties = [ ];
        propertiesByName = properties.ToFrozenDictionary(
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
    public IReadOnlyCollection<ObjectPropertySymbol> Properties => properties;

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

    /// <summary>Creates an anonymous structured object type inferred by the compiler.</summary>
    /// <param name="isOpen">A value indicating whether undeclared properties are permitted.</param>
    /// <param name="properties">The inferred object properties.</param>
    /// <returns>The anonymous structured object type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="properties" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a property name is duplicated.</exception>
    public static ObjectTypeSymbol CreateAnonymous(
        bool isOpen,
        IEnumerable<ObjectPropertySymbol> properties
    )
    {
        ObjectTypeSymbol type = new (null, "<anonymous>", isOpen, false);
        type.Complete(properties);
        return type;
    }

    internal static ObjectTypeSymbol CreateIncomplete(
        string? id,
        string name,
        bool isOpen
    )
    {
        return new ObjectTypeSymbol(id, name, isOpen, id is not null);
    }

    internal void Complete(IEnumerable<ObjectPropertySymbol> properties)
    {
        if (isComplete)
        {
            throw new InvalidOperationException(
                $"Structured type '{DisplayName}' is already complete."
            );
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

        this.properties = propertyList;
        propertiesByName = propertyList.ToFrozenDictionary(
            static property => property.Name,
            StringComparer.Ordinal
        );
        isComplete = true;
    }
}
