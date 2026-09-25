using MuLang.Core.Types;
using System.Globalization;
using System.Text;

namespace MuLang.IR.Serialization;

internal static class MuIrTypeTableBuilder
{
    public static (
        IReadOnlyList<TypeSymbol> Types,
        IReadOnlyDictionary<TypeSymbol, int> TypeIds
        ) Build(IrProgram program)
    {
        Collector collector = new ();
        collector.AddProgram(program);
        return collector.Build();
    }

    private sealed class Collector
    {
        private readonly IDictionary<TypeSymbol, TypeNode> nodesByType =
            new Dictionary<TypeSymbol, TypeNode>(ReferenceEqualityComparer.Instance);

        private readonly IList<TypeNode> nodes = new List<TypeNode>();
        private readonly IList<TypeNode> roots = new List<TypeNode>();

        public void AddProgram(IrProgram program)
        {
            AddFunction(program.EntryFunction);

            foreach (IrFunction function in program.UserFunctions)
            {
                AddFunction(function);
            }
        }

        public (
            IReadOnlyList<TypeSymbol>,
            IReadOnlyDictionary<TypeSymbol, int>
            ) Build()
        {
            RefinePartitions();
            IReadOnlyList<int> colors = OrderColors();
            IReadOnlyDictionary<int, int> idsByColor = colors
                .Select(static (color, id) => (color, id))
                .ToDictionary(static item => item.color, static item => item.id);
            IReadOnlyList<TypeSymbol> types =
            [
                .. colors.Select(
                    color => nodes.First(node => node.Color == color).Type
                ),
            ];
            Dictionary<TypeSymbol, int> typeIds =
                new (ReferenceEqualityComparer.Instance);

            foreach (KeyValuePair<TypeSymbol, TypeNode> item in nodesByType)
            {
                typeIds.Add(item.Key, idsByColor[item.Value.Color]);
            }

            return (types, typeIds);
        }

        private void AddFunction(IrFunction function)
        {
            AddRoot(function.ReturnType);

            foreach (IrSlot slot in function.Slots)
            {
                AddRoot(slot.Type);
            }

            foreach (IrInstruction instruction in function.Blocks.SelectMany(
                    static block => block.Instructions
                ))
            {
                switch (instruction)
                {
                    case IrInstruction.Constant value:
                        AddRoot(value.Type);
                        break;

                    case IrInstruction.Convert value:
                        AddRoot(value.TargetType);
                        break;

                    case IrInstruction.TypeTest value:
                        AddRoot(value.TestedType);
                        break;

                    case IrInstruction.CreateArray value:
                        AddRoot(value.Type);
                        break;

                    case IrInstruction.CreateObject value:
                        AddRoot(value.Type);
                        break;

                    case IrInstruction.ProviderCall value:
                        AddRoot(value.ReturnType);
                        break;

                    case IrInstruction.UserCall value:
                        AddRoot(value.ReturnType);
                        break;
                }
            }
        }

        private void AddRoot(TypeSymbol type)
        {
            roots.Add(Add(type));
        }

        private TypeNode Add(TypeSymbol type)
        {
            if (nodesByType.TryGetValue(type, out TypeNode? existing))
            {
                return existing;
            }

            if (type.Kind == TypeKind.Error)
            {
                throw new MuIrSerializationException(
                    "The compiler error-recovery type cannot be serialized."
                );
            }

            TypeNode node = new (
                type,
                nodes.Count,
                GetScalarKey(type)
            );
            nodesByType.Add(type, node);
            nodes.Add(node);

            switch (type)
            {
                case NullableTypeSymbol nullable:
                    node.Edges.Add(new TypeEdge("underlying", Add(nullable.UnderlyingType)));
                    break;

                case ArrayTypeSymbol array:
                    node.Edges.Add(new TypeEdge("element", Add(array.ElementType)));
                    break;

                case ObjectTypeSymbol structured:
                    foreach (ObjectPropertySymbol property in structured.Properties.OrderBy(
                            static property => property.Name,
                            StringComparer.Ordinal
                        ))
                    {
                        node.Edges.Add(
                            new TypeEdge(
                                GetPropertyEdgeKey(property),
                                Add(property.Type)
                            )
                        );
                    }

                    break;
            }

            return node;
        }

        private void RefinePartitions()
        {
            AssignColors(
                nodes.ToDictionary(
                    static node => node,
                    static node => GetSignature(node, false)
                )
            );

            for (int iteration = 0; iteration <= nodes.Count; iteration++)
            {
                IReadOnlyDictionary<TypeNode, string> signatures =
                    nodes.ToDictionary(
                        static node => node,
                        static node => GetSignature(node, true)
                    );
                int[] previous = [ .. nodes.Select(static node => node.Color) ];
                AssignColors(signatures);

                if (nodes.Select(static node => node.Color).SequenceEqual(previous))
                {
                    foreach (TypeNode node in nodes)
                    {
                        node.Signature = signatures[node];
                    }

                    return;
                }
            }

            throw new MuIrSerializationException(
                "Type-graph canonicalization did not converge."
            );
        }

        private void AssignColors(
            IReadOnlyDictionary<TypeNode, string> signatures
        )
        {
            IReadOnlyDictionary<string, int> colors = signatures.Values
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(static (signature, color) => (signature, color))
                .ToDictionary(
                    static item => item.signature,
                    static item => item.color,
                    StringComparer.Ordinal
                );

            foreach (TypeNode node in nodes)
            {
                node.Color = colors[signatures[node]];
            }
        }

        private IReadOnlyList<int> OrderColors()
        {
            IReadOnlyDictionary<int, TypeNode> representatives = nodes
                .GroupBy(static node => node.Color)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.OrderBy(node => node.Encounter).First()
                );
            Dictionary<int, VisitState> states = [ ];
            List<int> ordered = [ ];

            void Visit(int color)
            {
                if (states.TryGetValue(color, out VisitState state))
                {
                    if (state != VisitState.Unvisited)
                    {
                        return;
                    }
                }

                states[color] = VisitState.Visiting;
                TypeNode node = representatives[color];

                foreach (int target in node.Edges
                    .Select(static edge => edge.Target.Color)
                    .Distinct()
                    .OrderBy(target => representatives[target].Signature, StringComparer.Ordinal))
                {
                    Visit(target);
                }

                states[color] = VisitState.Visited;

                if (!ordered.Contains(color))
                {
                    ordered.Add(color);
                }
            }

            foreach (TypeNode root in roots)
            {
                Visit(root.Color);
            }

            foreach (int color in representatives.Keys.OrderBy(
                    color => representatives[color].Signature,
                    StringComparer.Ordinal
                ))
            {
                Visit(color);
            }

            return ordered;
        }

        private static string GetSignature(TypeNode node, bool includeTargets)
        {
            StringBuilder builder = new ();
            Append(builder, node.ScalarKey);

            foreach (TypeEdge edge in node.Edges)
            {
                Append(builder, edge.Label);

                if (includeTargets)
                {
                    Append(
                        builder,
                        edge.Target.Color.ToString(CultureInfo.InvariantCulture)
                    );
                }
            }

            return builder.ToString();
        }

        private static string GetScalarKey(TypeSymbol type)
        {
            StringBuilder builder = new ();

            switch (type)
            {
                case NullableTypeSymbol:
                    Append(builder, "nullable");
                    break;

                case ArrayTypeSymbol array:
                    Append(builder, "array");
                    Append(builder, array.IsReadOnly ? "readonly" : "mutable");
                    break;

                case ObjectTypeSymbol structured:
                    Append(builder, "object");
                    Append(builder, structured.IsOpen ? "open" : "closed");
                    Append(
                        builder,
                        structured.Properties.Count.ToString(CultureInfo.InvariantCulture)
                    );
                    break;

                default:
                    Append(builder, "intrinsic");
                    Append(builder, GetIntrinsicToken(type));
                    break;
            }

            return builder.ToString();
        }

        private static string GetIntrinsicToken(TypeSymbol type)
        {
            return type.Kind switch
            {
                TypeKind.Bool => "bool",
                TypeKind.Int => "int",
                TypeKind.Float => "float",
                TypeKind.Number => "number",
                TypeKind.String => "string",
                TypeKind.Unknown => "unknown",
                TypeKind.Object => "object",
                TypeKind.Void => "void",
                TypeKind.Null => "null",
                _ => throw new MuIrSerializationException(
                    $"Type '{type.DisplayName}' is not supported."
                ),
            };
        }

        private static string GetPropertyEdgeKey(ObjectPropertySymbol property)
        {
            StringBuilder builder = new ();
            Append(builder, property.Name);
            Append(builder, property.IsOptional ? "optional" : "required");
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append(';');
        }
    }

    private sealed class TypeNode
    {
        public TypeNode(TypeSymbol type, int encounter, string scalarKey)
        {
            Type = type;
            Encounter = encounter;
            ScalarKey = scalarKey;
        }

        public TypeSymbol Type { get; }

        public int Encounter { get; }

        public string ScalarKey { get; }

        public IList<TypeEdge> Edges { get; } = new List<TypeEdge>();

        public int Color { get; set; }

        public string Signature { get; set; } = "";
    }

    private sealed record TypeEdge(string Label, TypeNode Target);

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited,
    }
}
