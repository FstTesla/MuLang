using MuLang.Core.Types;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.IR;

/// <summary>Represents immutable well-known diagnostic attributes for an IR program.</summary>
public sealed class IrAttributeCollection
{
    /// <summary>Initializes a new instance of the <see cref="IrAttributeCollection" /> class.</summary>
    /// <param name="program">The program attributes, or <c>null</c> when absent.</param>
    /// <param name="functions">The function attributes keyed by portable function identifier.</param>
    /// <param name="slots">The slot attributes keyed by containing function and slot identifier.</param>
    /// <param name="types">The type attributes keyed by projected type reference.</param>
    /// <exception cref="ArgumentNullException">Thrown when a collection argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when a target is duplicated.</exception>
    public IrAttributeCollection(
        IrProgramAttributes? program,
        IEnumerable<KeyValuePair<string, IrFunctionAttributes>> functions,
        IEnumerable<KeyValuePair<IrSlotAttributeTarget, IrSlotAttributes>> slots,
        IEnumerable<KeyValuePair<TypeSymbol, IrTypeAttributes>> types
    )
    {
        if (functions is null)
        {
            throw new ArgumentNullException(nameof(functions));
        }

        if (slots is null)
        {
            throw new ArgumentNullException(nameof(slots));
        }

        if (types is null)
        {
            throw new ArgumentNullException(nameof(types));
        }

        Program = program is null
            ? null
            : program with
            {
                ProviderGlobalNames = [ .. program.ProviderGlobalNames ],
                ProviderFunctionNames = [ .. program.ProviderFunctionNames ],
            };
        Functions = new ReadOnlyDictionary<string, IrFunctionAttributes>(
            functions.ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal
            )
        );
        Slots = new ReadOnlyDictionary<IrSlotAttributeTarget, IrSlotAttributes>(
            slots.ToDictionary(static item => item.Key, static item => item.Value)
        );
        Dictionary<TypeSymbol, IrTypeAttributes> typeAttributes =
            new (ReferenceEqualityComparer.Instance);

        foreach (KeyValuePair<TypeSymbol, IrTypeAttributes> item in types)
        {
            IrTypeAttributes value = new ([ .. item.Value.SourceNames ]);

            if (!typeAttributes.TryAdd(item.Key, value))
            {
                throw new ArgumentException(
                    "A type attribute target is duplicated.",
                    nameof(types)
                );
            }
        }

        Types = new ReadOnlyDictionary<TypeSymbol, IrTypeAttributes>(
            typeAttributes
        );
    }

    /// <summary>Gets the reusable empty attribute collection.</summary>
    public static IrAttributeCollection Empty { get; } = new (
        null,
        [ ],
        [ ],
        [ ]
    );

    /// <summary>Gets the program attributes, or <c>null</c> when absent.</summary>
    public IrProgramAttributes? Program { get; }

    /// <summary>Gets function attributes keyed by portable function identifier.</summary>
    public IReadOnlyDictionary<string, IrFunctionAttributes> Functions { get; }

    /// <summary>Gets slot attributes keyed by containing function and slot identifier.</summary>
    public IReadOnlyDictionary<IrSlotAttributeTarget, IrSlotAttributes> Slots { get; }

    /// <summary>Gets type attributes keyed by projected type reference.</summary>
    public IReadOnlyDictionary<TypeSymbol, IrTypeAttributes> Types { get; }

    /// <summary>Gets the attributes for a function.</summary>
    /// <param name="functionId">The portable function identifier.</param>
    /// <param name="attributes">When this method returns, contains the function attributes, if found.</param>
    /// <returns><c>true</c> if attributes were found; otherwise, <c>false</c>.</returns>
    public bool TryGetFunction(
        string functionId,
        [NotNullWhen(true)] out IrFunctionAttributes? attributes
    )
    {
        return Functions.TryGetValue(functionId, out attributes);
    }

    /// <summary>Gets the attributes for a function-local slot.</summary>
    /// <param name="functionId">The portable function identifier.</param>
    /// <param name="slotId">The function-local slot identifier.</param>
    /// <param name="attributes">When this method returns, contains the slot attributes, if found.</param>
    /// <returns><c>true</c> if attributes were found; otherwise, <c>false</c>.</returns>
    public bool TryGetSlot(
        string functionId,
        int slotId,
        [NotNullWhen(true)] out IrSlotAttributes? attributes
    )
    {
        return Slots.TryGetValue(
            new IrSlotAttributeTarget(functionId, slotId),
            out attributes
        );
    }

    /// <summary>Gets the attributes for a projected type.</summary>
    /// <param name="type">The projected type.</param>
    /// <param name="attributes">When this method returns, contains the type attributes, if found.</param>
    /// <returns><c>true</c> if attributes were found; otherwise, <c>false</c>.</returns>
    public bool TryGetType(
        TypeSymbol type,
        [NotNullWhen(true)] out IrTypeAttributes? attributes
    )
    {
        return Types.TryGetValue(type, out attributes);
    }
}
