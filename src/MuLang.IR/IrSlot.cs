using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Describes a value slot in a MuLang IR function.</summary>
public sealed record IrSlot
{
    /// <summary>Initializes a new instance of the <see cref="IrSlot" /> class.</summary>
    /// <param name="id">The slot identifier within the function.</param>
    /// <param name="kind">The slot kind.</param>
    /// <param name="type">The static slot type.</param>
    /// <param name="mutability">The slot mutability.</param>
    /// <param name="lifetimeRegion">The owning lifetime-region identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type" /> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="kind" /> or <paramref name="mutability" /> is not defined, or when an identifier is negative.</exception>
    public IrSlot(
        int id,
        IrSlotKind kind,
        TypeSymbol type,
        IrSlotMutability mutability,
        int lifetimeRegion
    )
    {
        if (id < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (!Enum.IsDefined(mutability))
        {
            throw new ArgumentOutOfRangeException(nameof(mutability));
        }

        if (lifetimeRegion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetimeRegion));
        }

        Id = id;
        Kind = kind;
        Type = type;
        Mutability = mutability;
        LifetimeRegion = lifetimeRegion;
    }

    /// <summary>Initializes a new instance of the <see cref="IrSlot" /> class. The <paramref name="Name" /> value is ignored.</summary>
    /// <param name="Id">The slot identifier within the function.</param>
    /// <param name="Kind">The slot kind.</param>
    /// <param name="Type">The static slot type.</param>
    /// <param name="Name">An ignored compatibility value. Use <see cref="IrProgram.Attributes" /> for source names.</param>
    /// <param name="Mutability">The slot mutability.</param>
    /// <param name="LifetimeRegion">The owning lifetime-region identifier.</param>
    [Obsolete("The Name value is ignored. Use IrProgram.Attributes slot attributes instead.")]
    public IrSlot(
        int Id,
        IrSlotKind Kind,
        TypeSymbol Type,
        string? Name,
        IrSlotMutability Mutability,
        int LifetimeRegion
    )
        : this(Id, Kind, Type, Mutability, LifetimeRegion) { }

    /// <summary>Initializes a new instance of the <see cref="IrSlot" /> class using default mutability and lifetime ownership. The <paramref name="Name" /> value is ignored.</summary>
    /// <param name="Id">The slot identifier within the function.</param>
    /// <param name="Kind">The slot kind.</param>
    /// <param name="Type">The static slot type.</param>
    /// <param name="Name">An ignored compatibility value. Use <see cref="IrProgram.Attributes" /> for source names.</param>
    [Obsolete("The Name value is ignored. Use the constructor without Name and IrProgram.Attributes slot attributes instead.")]
    public IrSlot(
        int Id,
        IrSlotKind Kind,
        TypeSymbol Type,
        string? Name
    )
        : this(
            Id,
            Kind,
            Type,
            Kind == IrSlotKind.Parameter
                ? IrSlotMutability.ReadOnly
                : IrSlotMutability.Mutable,
            0
        ) { }

    /// <summary>Initializes a new instance of the <see cref="IrSlot" /> class with explicit mutability. The <paramref name="name" /> value is ignored.</summary>
    /// <param name="id">The slot identifier within the function.</param>
    /// <param name="kind">The slot kind.</param>
    /// <param name="type">The static slot type.</param>
    /// <param name="name">An ignored compatibility value. Use <see cref="IrProgram.Attributes" /> for source names.</param>
    /// <param name="mutability">The slot mutability.</param>
    [Obsolete("The name value is ignored. Use the constructor without name and IrProgram.Attributes slot attributes instead.")]
    public IrSlot(
        int id,
        IrSlotKind kind,
        TypeSymbol type,
        string? name,
        IrSlotMutability mutability
    )
        : this(id, kind, type, mutability, 0) { }

    /// <summary>Gets the slot identifier within the function.</summary>
    public int Id { get; init; }

    /// <summary>Gets the slot kind.</summary>
    public IrSlotKind Kind { get; init; }

    /// <summary>Gets the static slot type.</summary>
    public TypeSymbol Type { get; init; }

    /// <summary>Gets an unused compatibility value. Source names are stored in <see cref="IrProgram.Attributes" />.</summary>
    [Obsolete("This property is unused. Use IrProgram.Attributes slot attributes instead.")]
    public string? Name
    {
        get => null;
        // ReSharper disable once ValueParameterNotUsed
        init { }
    }

    /// <summary>Gets the slot mutability.</summary>
    public IrSlotMutability Mutability { get; init; }

    /// <summary>Gets the owning lifetime-region identifier.</summary>
    public int LifetimeRegion { get; init; }

    /// <summary>Deconstructs the slot without the unused compatibility name.</summary>
    /// <param name="Id">The slot identifier within the function.</param>
    /// <param name="Kind">The slot kind.</param>
    /// <param name="Type">The static slot type.</param>
    /// <param name="Mutability">The slot mutability.</param>
    /// <param name="LifetimeRegion">The owning lifetime-region identifier.</param>
    public void Deconstruct(
        out int Id,
        out IrSlotKind Kind,
        out TypeSymbol Type,
        out IrSlotMutability Mutability,
        out int LifetimeRegion
    )
    {
        Id = this.Id;
        Kind = this.Kind;
        Type = this.Type;
        Mutability = this.Mutability;
        LifetimeRegion = this.LifetimeRegion;
    }

    /// <summary>Deconstructs the slot using the legacy component shape. The returned name is always <c>null</c>.</summary>
    /// <param name="Id">The slot identifier within the function.</param>
    /// <param name="Kind">The slot kind.</param>
    /// <param name="Type">The static slot type.</param>
    /// <param name="Name">Always <c>null</c>. Use <see cref="IrProgram.Attributes" /> for source names.</param>
    [Obsolete("The Name value is always null. Use the deconstruction shape without Name and IrProgram.Attributes slot attributes instead.")]
    public void Deconstruct(
        out int Id,
        out IrSlotKind Kind,
        out TypeSymbol Type,
        out string? Name
    )
    {
        Id = this.Id;
        Kind = this.Kind;
        Type = this.Type;
        Name = null;
    }

    /// <summary>Deconstructs the slot using the legacy complete component shape. The returned name is always <c>null</c>.</summary>
    /// <param name="Id">The slot identifier within the function.</param>
    /// <param name="Kind">The slot kind.</param>
    /// <param name="Type">The static slot type.</param>
    /// <param name="Name">Always <c>null</c>. Use <see cref="IrProgram.Attributes" /> for source names.</param>
    /// <param name="Mutability">The slot mutability.</param>
    /// <param name="LifetimeRegion">The owning lifetime-region identifier.</param>
    [Obsolete("The Name value is always null. Use the deconstruction shape without Name and IrProgram.Attributes slot attributes instead.")]
    public void Deconstruct(
        out int Id,
        out IrSlotKind Kind,
        out TypeSymbol Type,
        out string? Name,
        out IrSlotMutability Mutability,
        out int LifetimeRegion
    )
    {
        Id = this.Id;
        Kind = this.Kind;
        Type = this.Type;
        Name = null;
        Mutability = this.Mutability;
        LifetimeRegion = this.LifetimeRegion;
    }
}
