using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Describes a value slot in a MuLang IR function.</summary>
/// <param name="Id">The slot identifier within the function.</param>
/// <param name="Kind">The slot kind.</param>
/// <param name="Type">The static slot type.</param>
/// <param name="Name">The source-level name, when available.</param>
/// <param name="Mutability">The slot mutability.</param>
/// <param name="LifetimeRegion">The owning lifetime-region identifier.</param>
public sealed record IrSlot(
    int Id,
    IrSlotKind Kind,
    TypeSymbol Type,
    string? Name,
    IrSlotMutability Mutability,
    int LifetimeRegion
)
{
    /// <summary>Initializes a new instance of the <see cref="IrSlot" /> class using default mutability and lifetime ownership.</summary>
    /// <param name="Id">The slot identifier within the function.</param>
    /// <param name="Kind">The slot kind.</param>
    /// <param name="Type">The static slot type.</param>
    /// <param name="Name">The source-level name, when available.</param>
    [Obsolete("Use the constructor that includes mutability and lifetime ownership.")]
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
            Name,
            Kind == IrSlotKind.Parameter
                ? IrSlotMutability.ReadOnly
                : IrSlotMutability.Mutable,
            0
        )
    {
    }

    /// <summary>Initializes a new instance of the <see cref="IrSlot" /> class with explicit mutability.</summary>
    /// <param name="id">The slot identifier within the function.</param>
    /// <param name="kind">The slot kind.</param>
    /// <param name="type">The static slot type.</param>
    /// <param name="name">The source-level name, when available.</param>
    /// <param name="mutability">The slot mutability.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mutability" /> is not defined.</exception>
    [Obsolete("Use the constructor that includes lifetime ownership.")]
    public IrSlot(
        int id,
        IrSlotKind kind,
        TypeSymbol type,
        string? name,
        IrSlotMutability mutability
    )
        : this(id, kind, type, name, mutability, 0)
    {
    }

    /// <summary>Deconstructs the slot using the legacy component shape.</summary>
    /// <param name="Id">The slot identifier within the function.</param>
    /// <param name="Kind">The slot kind.</param>
    /// <param name="Type">The static slot type.</param>
    /// <param name="Name">The source-level name, when available.</param>
    [Obsolete("Use the deconstruction shape that includes mutability and lifetime ownership.")]
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
        Name = this.Name;
    }

    /// <summary>Gets the slot mutability.</summary>
    public IrSlotMutability Mutability { get; init; } = ValidateMutability(Mutability);

    private static IrSlotMutability ValidateMutability(IrSlotMutability mutability)
    {
        if (!Enum.IsDefined(mutability))
        {
            throw new ArgumentOutOfRangeException(nameof(mutability));
        }

        return mutability;
    }
}
