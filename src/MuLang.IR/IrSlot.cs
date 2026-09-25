using MuLang.Core.Types;

namespace MuLang.IR;

/// <summary>Describes a value slot in a MuLang IR function.</summary>
/// <param name="Id">The slot identifier within the function.</param>
/// <param name="Kind">The slot kind.</param>
/// <param name="Type">The static slot type.</param>
/// <param name="Name">The source-level name, when available.</param>
public sealed record IrSlot(
    int Id,
    IrSlotKind Kind,
    TypeSymbol Type,
    string? Name
)
{
    /// <summary>Initializes a new instance of the <see cref="IrSlot" /> class with explicit mutability.</summary>
    /// <param name="id">The slot identifier within the function.</param>
    /// <param name="kind">The slot kind.</param>
    /// <param name="type">The static slot type.</param>
    /// <param name="name">The source-level name, when available.</param>
    /// <param name="mutability">The slot mutability.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mutability" /> is not defined.</exception>
    public IrSlot(
        int id,
        IrSlotKind kind,
        TypeSymbol type,
        string? name,
        IrSlotMutability mutability
    )
        : this(id, kind, type, name)
    {
        if (!Enum.IsDefined(mutability))
        {
            throw new ArgumentOutOfRangeException(nameof(mutability));
        }

        Mutability = mutability;
    }

    /// <summary>Gets the slot mutability.</summary>
    public IrSlotMutability Mutability { get; init; } =
        Kind == IrSlotKind.Parameter
            ? IrSlotMutability.ReadOnly
            : IrSlotMutability.Mutable;
}
