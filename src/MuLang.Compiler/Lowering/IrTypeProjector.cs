using MuLang.Core.Types;
using MuLang.IR;

namespace MuLang.Compiler.Lowering;

internal static class IrTypeProjector
{
    public static IrProgram Project(IrProgram program)
    {
        ObjectTypeGraphBuilder builder = new();
        Dictionary<TypeSymbol, ObjectTypeGraphReference> references =
            new(ReferenceEqualityComparer.Instance);
        int objectIndex = 0;

        ObjectTypeGraphReference Add(TypeSymbol type)
        {
            if (
                references.TryGetValue(
                    type,
                    out ObjectTypeGraphReference? existing
                )
            )
            {
                return existing;
            }

            ObjectTypeGraphReference reference;

            switch (type)
            {
                case ObjectTypeSymbol structured:
                    {
                        reference = builder.DeclareAnonymous(
                            $"object{objectIndex++}",
                            structured.IsOpen
                        );
                        references.Add(type, reference);

                        foreach (ObjectPropertySymbol property in structured.Properties.OrderBy(
                                static property => property.Name,
                                StringComparer.Ordinal
                            ))
                        {
                            builder.AddProperty(
                                reference,
                                property.Name,
                                Add(property.Type),
                                property.IsOptional
                            );
                        }

                        return reference;
                    }

                case NullableTypeSymbol nullable:
                    reference = builder.Nullable(Add(nullable.UnderlyingType));
                    break;

                case ArrayTypeSymbol { IsReadOnly: true } array:
                    reference = builder.ReadOnlyArray(Add(array.ElementType));
                    break;

                case ArrayTypeSymbol array:
                    reference = builder.Array(Add(array.ElementType));
                    break;

                default:
                    reference = builder.From(type);
                    break;
            }

            references.Add(type, reference);
            return reference;
        }

        void AddFunction(IrFunction function)
        {
            _ = Add(function.ReturnType);

            foreach (IrSlot slot in function.Slots)
            {
                _ = Add(slot.Type);
            }

            foreach (IrInstruction instruction in function.Blocks.SelectMany(
                    static block => block.Instructions
                ))
            {
                switch (instruction)
                {
                    case IrInstruction.Constant value:
                        _ = Add(value.Type);
                        break;

                    case IrInstruction.Convert value:
                        _ = Add(value.TargetType);
                        break;

                    case IrInstruction.TypeTest value:
                        _ = Add(value.TestedType);
                        break;

                    case IrInstruction.CreateArray value:
                        _ = Add(value.Type);
                        break;

                    case IrInstruction.CreateObject value:
                        _ = Add(value.Type);
                        break;

                    case IrInstruction.ProviderCall value:
                        _ = Add(value.ReturnType);
                        break;

                    case IrInstruction.UserCall value:
                        _ = Add(value.ReturnType);
                        break;
                }
            }
        }

        AddFunction(program.EntryFunction);

        foreach (IrFunction function in program.UserFunctions)
        {
            AddFunction(function);
        }

        IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> projected =
            builder.Build();

        TypeSymbol Get(TypeSymbol type) => projected[references[type]];

        IrFunction ProjectFunction(IrFunction function)
        {
            return new IrFunction(
                function.Id,
                Get(function.ReturnType),
                function.EntryBlock,
                [
                    .. function.Slots.Select(
                        slot => slot with { Type = Get(slot.Type) }
                    ),
                ],
                [
                    .. function.Blocks.Select(
                        block => new IrBasicBlock(
                            block.Id,
                            [
                                .. block.Instructions.Select(
                                    ProjectInstruction
                                ),
                            ],
                            block.Terminator
                        )
                    ),
                ]
            );
        }

        IrInstruction ProjectInstruction(IrInstruction instruction)
        {
            return instruction switch
            {
                IrInstruction.Constant value =>
                    value with { Type = Get(value.Type) },
                IrInstruction.Convert value =>
                    value with { TargetType = Get(value.TargetType) },
                IrInstruction.TypeTest value =>
                    value with { TestedType = Get(value.TestedType) },
                IrInstruction.CreateArray value =>
                    value with { Type = (ArrayTypeSymbol)Get(value.Type) },
                IrInstruction.CreateObject value =>
                    value with { Type = (ObjectTypeSymbol)Get(value.Type) },
                IrInstruction.ProviderCall value =>
                    value with { ReturnType = Get(value.ReturnType) },
                IrInstruction.UserCall value =>
                    value with { ReturnType = Get(value.ReturnType) },
                _ => instruction,
            };
        }

        return new IrProgram(
            program.EnvironmentFingerprint,
            program.CompilationMode,
            program.LanguageProfileFingerprint,
            ProjectFunction(program.EntryFunction),
            [.. program.UserFunctions.Select(ProjectFunction)]
        );
    }
}
