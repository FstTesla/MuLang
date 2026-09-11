using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.Core.Types;

public sealed class ObjectTypeSymbol : TypeSymbol
{
    private readonly IReadOnlyDictionary<string, ObjectPropertySymbol> propertiesByName;

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

        IReadOnlyList<ObjectPropertySymbol> propertyList = [ .. properties ];

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

    public string? Id { get; }

    public string Name { get; }

    public bool IsOpen { get; }

    public IReadOnlyCollection<ObjectPropertySymbol> Properties { get; }

    public override string DisplayName => Name;

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
