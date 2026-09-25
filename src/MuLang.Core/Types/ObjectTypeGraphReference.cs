namespace MuLang.Core.Types;

/// <summary>Represents an opaque type expression in an object-type graph builder.</summary>
public sealed class ObjectTypeGraphReference
{
    internal ObjectTypeGraphReference(
        ObjectTypeGraphBuilder owner,
        ObjectTypeGraphReferenceKind kind,
        string? key,
        string? id,
        string? name,
        bool isOpen,
        TypeSymbol? existingType,
        ObjectTypeGraphReference? elementType,
        bool isReadOnly
    )
    {
        Owner = owner;
        Kind = kind;
        Key = key;
        Id = id;
        Name = name;
        IsOpen = isOpen;
        ExistingType = existingType;
        ElementType = elementType;
        IsReadOnly = isReadOnly;
    }

    internal ObjectTypeGraphBuilder Owner { get; }

    internal ObjectTypeGraphReferenceKind Kind { get; }

    internal string? Key { get; }

    internal string? Id { get; }

    internal string? Name { get; }

    internal bool IsOpen { get; }

    internal TypeSymbol? ExistingType { get; }

    internal ObjectTypeGraphReference? ElementType { get; }

    internal bool IsReadOnly { get; }

    internal IList<ObjectTypeGraphProperty> Properties { get; } =
        new List<ObjectTypeGraphProperty>();
}

internal enum ObjectTypeGraphReferenceKind
{
    Existing,
    Object,
    Nullable,
    Array,
}

internal sealed record ObjectTypeGraphProperty(
    string Name,
    ObjectTypeGraphReference Type,
    bool IsOptional
);
