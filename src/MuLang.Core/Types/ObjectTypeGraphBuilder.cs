using System.Collections.ObjectModel;

namespace MuLang.Core.Types;

/// <summary>Builds immutable object-type graphs, including recursive graphs.</summary>
public sealed class ObjectTypeGraphBuilder
{
    private readonly IDictionary<string, ObjectTypeGraphReference> objectsByKey =
        new Dictionary<string, ObjectTypeGraphReference>(StringComparer.Ordinal);

    private readonly IList<ObjectTypeGraphReference> references =
        new List<ObjectTypeGraphReference>();

    private bool isBuilt;

    /// <summary>Declares a named object type.</summary>
    /// <param name="key">The builder-local key.</param>
    /// <param name="id">The provider type identifier.</param>
    /// <param name="name">The language-facing name.</param>
    /// <param name="isOpen">Whether undeclared properties are permitted.</param>
    /// <returns>An opaque reference to the declared type.</returns>
    /// <exception cref="ArgumentException">Thrown when a value is invalid or duplicated.</exception>
    /// <exception cref="InvalidOperationException">Thrown after the builder has been finalized.</exception>
    public ObjectTypeGraphReference DeclareNamed(
        string key,
        string id,
        string name,
        bool isOpen
    )
    {
        EnsureMutable();
        ValidateKey(key);

        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException(
                "Type identifier cannot be null or whitespace.",
                nameof(id)
            );
        }

        LanguageNames.ValidateIdentifier(name, nameof(name));

        return DeclareObject(key, id, name, isOpen);
    }

    /// <summary>Declares an anonymous object type.</summary>
    /// <param name="key">The builder-local key.</param>
    /// <param name="isOpen">Whether undeclared properties are permitted.</param>
    /// <returns>An opaque reference to the declared type.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key" /> is invalid or duplicated.</exception>
    /// <exception cref="InvalidOperationException">Thrown after the builder has been finalized.</exception>
    public ObjectTypeGraphReference DeclareAnonymous(string key, bool isOpen)
    {
        EnsureMutable();
        ValidateKey(key);
        return DeclareObject(key, null, "<anonymous>", isOpen);
    }

    /// <summary>Creates a reference to an existing type.</summary>
    /// <param name="type">The existing type.</param>
    /// <returns>An opaque reference to the type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown after the builder has been finalized.</exception>
    public ObjectTypeGraphReference From(TypeSymbol type)
    {
        EnsureMutable();

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return AddReference(
            new ObjectTypeGraphReference(
                this,
                ObjectTypeGraphReferenceKind.Existing,
                null,
                null,
                null,
                false,
                type,
                null,
                false
            )
        );
    }

    /// <summary>Creates a nullable type expression.</summary>
    /// <param name="underlyingType">The underlying type expression.</param>
    /// <returns>An opaque reference to the nullable type.</returns>
    /// <exception cref="ArgumentException">Thrown when the reference belongs to another builder.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlyingType" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown after the builder has been finalized.</exception>
    public ObjectTypeGraphReference Nullable(ObjectTypeGraphReference underlyingType)
    {
        return AddComposite(
            ObjectTypeGraphReferenceKind.Nullable,
            underlyingType,
            false
        );
    }

    /// <summary>Creates a mutable-array type expression.</summary>
    /// <param name="elementType">The element type expression.</param>
    /// <returns>An opaque reference to the mutable-array type.</returns>
    /// <exception cref="ArgumentException">Thrown when the reference belongs to another builder.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elementType" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown after the builder has been finalized.</exception>
    public ObjectTypeGraphReference Array(ObjectTypeGraphReference elementType)
    {
        return AddComposite(
            ObjectTypeGraphReferenceKind.Array,
            elementType,
            false
        );
    }

    /// <summary>Creates a read-only-array type expression.</summary>
    /// <param name="elementType">The element type expression.</param>
    /// <returns>An opaque reference to the read-only-array type.</returns>
    /// <exception cref="ArgumentException">Thrown when the reference belongs to another builder.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="elementType" /> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown after the builder has been finalized.</exception>
    public ObjectTypeGraphReference ReadOnlyArray(
        ObjectTypeGraphReference elementType
    )
    {
        return AddComposite(
            ObjectTypeGraphReferenceKind.Array,
            elementType,
            true
        );
    }

    /// <summary>Adds a property to a declared object type.</summary>
    /// <param name="objectType">The declared object type.</param>
    /// <param name="name">The property name.</param>
    /// <param name="propertyType">The property type expression.</param>
    /// <param name="isOptional">Whether the property may be omitted.</param>
    /// <returns>The same <see cref="ObjectTypeGraphBuilder" /> instance, for chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when a reference is invalid or the property is duplicated.</exception>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown after the builder has been finalized.</exception>
    public ObjectTypeGraphBuilder AddProperty(
        ObjectTypeGraphReference objectType,
        string name,
        ObjectTypeGraphReference propertyType,
        bool isOptional = false
    )
    {
        EnsureMutable();
        ValidateReference(objectType, nameof(objectType));
        ValidateReference(propertyType, nameof(propertyType));

        if (objectType.Kind != ObjectTypeGraphReferenceKind.Object)
        {
            throw new ArgumentException(
                "Properties can only be added to declared object types.",
                nameof(objectType)
            );
        }

        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (
            objectType.Properties.Any(
                property => property.Name.Equals(name, StringComparison.Ordinal)
            )
        )
        {
            throw new ArgumentException(
                $"Property '{name}' is declared more than once.",
                nameof(name)
            );
        }

        objectType.Properties.Add(
            new ObjectTypeGraphProperty(name, propertyType, isOptional)
        );
        return this;
    }

    /// <summary>Finalizes the complete type graph.</summary>
    /// <returns>An immutable mapping from graph references to completed types.</returns>
    /// <exception cref="ArgumentException">Thrown when a type expression is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the builder was already finalized.</exception>
    public IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> Build()
    {
        EnsureMutable();
        isBuilt = true;
        Dictionary<ObjectTypeGraphReference, TypeSymbol> types =
            new (ReferenceEqualityComparer.Instance);

        foreach (ObjectTypeGraphReference reference in objectsByKey.Values)
        {
            types.Add(
                reference,
                ObjectTypeSymbol.CreateIncomplete(
                    reference.Id,
                    reference.Name!,
                    reference.IsOpen
                )
            );
        }

        TypeSymbol Materialize(ObjectTypeGraphReference reference)
        {
            if (types.TryGetValue(reference, out TypeSymbol? existing))
            {
                return existing;
            }

            TypeSymbol value = reference.Kind switch
            {
                ObjectTypeGraphReferenceKind.Existing =>
                    reference.ExistingType!,
                ObjectTypeGraphReferenceKind.Nullable =>
                    TypeSymbols.Nullable(Materialize(reference.ElementType!)),
                ObjectTypeGraphReferenceKind.Array when reference.IsReadOnly =>
                    TypeSymbols.ReadOnlyArray(Materialize(reference.ElementType!)),
                ObjectTypeGraphReferenceKind.Array =>
                    TypeSymbols.Array(Materialize(reference.ElementType!)),
                _ => throw new InvalidOperationException(
                    "The object-type graph contains an invalid reference."
                ),
            };
            types.Add(reference, value);
            return value;
        }

        foreach (ObjectTypeGraphReference reference in references)
        {
            _ = Materialize(reference);
        }

        foreach (ObjectTypeGraphReference reference in objectsByKey.Values)
        {
            ObjectTypeSymbol type = (ObjectTypeSymbol)types[reference];
            type.Complete(
                reference.Properties.Select(
                    property => new ObjectPropertySymbol(
                        property.Name,
                        Materialize(property.Type),
                        property.IsOptional
                    )
                )
            );
        }

        return new ReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol>(types);
    }

    private ObjectTypeGraphReference DeclareObject(
        string key,
        string? id,
        string name,
        bool isOpen
    )
    {
        if (objectsByKey.ContainsKey(key))
        {
            throw new ArgumentException($"Duplicate builder key '{key}'.", nameof(key));
        }

        ObjectTypeGraphReference reference = AddReference(
            new ObjectTypeGraphReference(
                this,
                ObjectTypeGraphReferenceKind.Object,
                key,
                id,
                name,
                isOpen,
                null,
                null,
                false
            )
        );
        objectsByKey.Add(key, reference);
        return reference;
    }

    private ObjectTypeGraphReference AddComposite(
        ObjectTypeGraphReferenceKind kind,
        ObjectTypeGraphReference elementType,
        bool isReadOnly
    )
    {
        EnsureMutable();
        ValidateReference(elementType, nameof(elementType));
        return AddReference(
            new ObjectTypeGraphReference(
                this,
                kind,
                null,
                null,
                null,
                false,
                null,
                elementType,
                isReadOnly
            )
        );
    }

    private ObjectTypeGraphReference AddReference(
        ObjectTypeGraphReference reference
    )
    {
        references.Add(reference);
        return reference;
    }

    private void ValidateReference(
        ObjectTypeGraphReference reference,
        string parameterName
    )
    {
        if (reference is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (!ReferenceEquals(reference.Owner, this))
        {
            throw new ArgumentException(
                "The type reference belongs to another builder.",
                parameterName
            );
        }
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Builder key cannot be null or whitespace.",
                nameof(key)
            );
        }
    }

    private void EnsureMutable()
    {
        if (isBuilt)
        {
            throw new InvalidOperationException(
                "The object-type graph builder is already finalized."
            );
        }
    }
}
