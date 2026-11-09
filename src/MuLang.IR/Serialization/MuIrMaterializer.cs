using MuLang.Core;
using MuLang.Core.Environment;
using MuLang.Core.Types;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.IR.Serialization;

internal sealed class MuIrMaterializer
{
    private readonly IReadOnlyList<TypeDescriptor> typeDescriptors;
    private readonly IList<TypeSymbol> types;
    private readonly MuIrReaderOptions options;
    private readonly Action<string, string> fail;

    public MuIrMaterializer(
        IReadOnlyList<TypeDescriptor> typeDescriptors,
        IList<TypeSymbol> types,
        MuIrReaderOptions options,
        Action<string, string> fail
    )
    {
        this.typeDescriptors = typeDescriptors;
        this.types = types;
        this.options = options;
        this.fail = fail;
    }

    public IrProgram MaterializeProgram(
        string environment,
        CompilationMode mode,
        string profile,
        IrFunction entry,
        IReadOnlyList<IrFunction> users,
        IrAttributeCollection attributes
    )
    {
        try
        {
            return new IrProgram(
                new EnvironmentFingerprint(environment),
                mode,
                new LanguageProfileFingerprint(profile),
                entry,
                users
            )
            {
                Attributes = attributes,
            };
        }
        catch (ArgumentException exception)
        {
            Fail(MuIrDiagnosticCodes.MaterializationFailed, exception.Message);
            throw;
        }
    }

    public void MaterializeTypes()
    {
        ValidateTypeNesting();
        ObjectTypeGraphBuilder builder = new ();
        ObjectTypeGraphReference?[] references =
            new ObjectTypeGraphReference?[typeDescriptors.Count];

        for (int index = 0; index < typeDescriptors.Count; index++)
        {
            switch (typeDescriptors[index])
            {
                case IntrinsicTypeDescriptor intrinsic:
                    references[index] = builder.From(intrinsic.Type);
                    break;

                case ObjectTypeDescriptor value:
                    references[index] = builder.DeclareAnonymous(
                        $"t{index}",
                        value.IsOpen
                    );
                    break;
            }
        }

        HashSet<int> active = [ ];

        ObjectTypeGraphReference Resolve(int id)
        {
            if (references[id] is { } resolved)
            {
                return resolved;
            }

            if (!active.Add(id))
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidType,
                    "A recursive type cycle must contain an object type."
                );
            }

            ObjectTypeGraphReference value = typeDescriptors[id] switch
            {
                NullableTypeDescriptor nullable =>
                    builder.Nullable(Resolve(nullable.UnderlyingType)),
                ArrayTypeDescriptor { IsReadOnly: true } array =>
                    builder.ReadOnlyArray(Resolve(array.ElementType)),
                ArrayTypeDescriptor array =>
                    builder.Array(Resolve(array.ElementType)),
                _ => throw new InvalidOperationException(
                    "The MuIR type descriptor is invalid."
                ),
            };
            active.Remove(id);
            references[id] = value;
            return value;
        }

        try
        {
            for (int index = 0; index < typeDescriptors.Count; index++)
            {
                _ = Resolve(index);
            }

            for (int index = 0; index < typeDescriptors.Count; index++)
            {
                if (typeDescriptors[index] is not ObjectTypeDescriptor objectType)
                {
                    continue;
                }

                foreach (ObjectPropertyDescriptor property in objectType.Properties)
                {
                    builder.AddProperty(
                        references[index]!,
                        property.Name,
                        Resolve(property.Type),
                        property.IsOptional,
                        property.IsReadOnly
                    );
                }
            }

            IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> result =
                builder.Build();

            foreach (ObjectTypeGraphReference? reference in references)
            {
                types.Add(result[reference!]);
            }
        }
        catch (ArgumentException exception)
        {
            Fail(MuIrDiagnosticCodes.InvalidType, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            Fail(MuIrDiagnosticCodes.MaterializationFailed, exception.Message);
        }
    }

    private void ValidateTypeNesting()
    {
        for (int index = 0; index < typeDescriptors.Count; index++)
        {
            Visit(index, 1, new HashSet<int>());
        }

        void Visit(int id, int depth, ISet<int> active)
        {
            if (depth > options.MaximumTypeNestingDepth)
            {
                Fail(
                    MuIrDiagnosticCodes.LimitExceeded,
                    $"Type 't{id}' exceeds the nesting-depth limit."
                );
            }

            if (!active.Add(id))
            {
                return;
            }

            switch (typeDescriptors[id])
            {
                case NullableTypeDescriptor nullable:
                    Visit(nullable.UnderlyingType, depth + 1, active);
                    break;

                case ArrayTypeDescriptor array:
                    Visit(array.ElementType, depth + 1, active);
                    break;

                case ObjectTypeDescriptor objectType:
                    foreach (ObjectPropertyDescriptor property in objectType.Properties)
                    {
                        Visit(property.Type, depth + 1, active);
                    }

                    break;
            }

            active.Remove(id);
        }
    }

    [DoesNotReturn]
    private void Fail(string code, string message)
    {
        fail(code, message);
        throw new UnreachableException();
    }

    internal abstract record TypeDescriptor;

    internal sealed record IntrinsicTypeDescriptor(TypeSymbol Type)
        : TypeDescriptor;

    internal sealed record NullableTypeDescriptor(int UnderlyingType)
        : TypeDescriptor;

    internal sealed record ArrayTypeDescriptor(bool IsReadOnly, int ElementType)
        : TypeDescriptor;

    internal sealed record ObjectTypeDescriptor(
        bool IsOpen,
        IReadOnlyList<ObjectPropertyDescriptor> Properties
    ) : TypeDescriptor;

    internal sealed record ObjectPropertyDescriptor(
        string Name,
        int Type,
        bool IsOptional,
        bool IsReadOnly
    );

}
