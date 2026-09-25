using MuLang.Core;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Globalization;
using System.Text;

namespace MuLang.IR.Serialization;

/// <summary>Provides canonical MuIR serialization.</summary>
public static class MuIrWriter
{
    /// <summary>Serializes a program to canonical MuIR text.</summary>
    /// <param name="program">The program to serialize.</param>
    /// <returns>The canonical MuIR document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="program" /> is <c>null</c>.</exception>
    /// <exception cref="MuIrSerializationException">Thrown when the program contains a value that MuIR cannot represent.</exception>
    public static string WriteToString(IrProgram program)
    {
        if (program is null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        StringBuilder builder = new ();
        using StringWriter writer = new (builder, CultureInfo.InvariantCulture);
        Write(program, writer);
        return builder.ToString();
    }

    /// <summary>Serializes a program to a text writer.</summary>
    /// <param name="program">The program to serialize.</param>
    /// <param name="writer">The destination writer.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="MuIrSerializationException">Thrown when the program contains a value that MuIR cannot represent.</exception>
    public static void Write(IrProgram program, TextWriter writer)
    {
        if (program is null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        new DocumentWriter(program, writer).Write();
    }

    /// <summary>Serializes a program as canonical UTF-8 MuIR.</summary>
    /// <param name="program">The program to serialize.</param>
    /// <param name="stream">The destination stream.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after writing.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <c>null</c>.</exception>
    /// <exception cref="MuIrSerializationException">Thrown when the program contains a value that MuIR cannot represent.</exception>
    public static void Write(
        IrProgram program,
        Stream stream,
        bool leaveOpen = true
    )
    {
        if (program is null)
        {
            throw new ArgumentNullException(nameof(program));
        }

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using StreamWriter writer = new (
            stream,
            new UTF8Encoding(false),
            1024,
            leaveOpen
        );
        Write(program, writer);
        writer.Flush();
    }

    private sealed class DocumentWriter
    {
        private readonly IrProgram program;
        private readonly TextWriter writer;
        private readonly IReadOnlyList<TypeSymbol> types;
        private readonly IReadOnlyDictionary<TypeSymbol, int> typeIds;
        private string currentFunctionId = "";
        private int currentBlockId;

        public DocumentWriter(IrProgram program, TextWriter writer)
        {
            this.program = program;
            this.writer = writer;
            (types, typeIds) = MuIrTypeTableBuilder.Build(program);
        }

        public void Write()
        {
            Line("muir 1");
            Line($"mode {GetCompilationMode(program.CompilationMode)}");
            Line($"environment {Quote(program.EnvironmentFingerprint.Value)}");
            Line($"profile {Quote(program.LanguageProfileFingerprint.Value)}");
            Line($"types {types.Count.ToString(CultureInfo.InvariantCulture)}");

            for (int index = 0; index < types.Count; index++)
            {
                WriteType(index, types[index]);
            }

            WriteFunction("entry", program.EntryFunction);
            Line($"users {program.UserFunctions.Count.ToString(CultureInfo.InvariantCulture)}");

            foreach (IrFunction function in program.UserFunctions)
            {
                WriteFunction("user", function);
            }

            Line("end");
        }

        private void WriteType(int id, TypeSymbol type)
        {
            string prefix = $"type t{id.ToString(CultureInfo.InvariantCulture)} ";

            switch (type)
            {
                case NullableTypeSymbol nullable:
                    Line($"{prefix}nullable {TypeReference(nullable.UnderlyingType)}");
                    break;

                case ArrayTypeSymbol array:
                    Line(
                        $"{prefix}array {(array.IsReadOnly ? "readonly" : "mutable")} " +
                        TypeReference(array.ElementType)
                    );
                    break;

                case ObjectTypeSymbol structured:
                    WriteObjectType(prefix, structured);
                    break;

                default:
                    Line($"{prefix}intrinsic {GetIntrinsicType(type)}");
                    break;
            }
        }

        private void WriteObjectType(string prefix, ObjectTypeSymbol type)
        {
            StringBuilder line = new ();
            line.Append(prefix);
            line.Append("object");
            line.Append(type.IsOpen ? " open " : " closed ");
            line.Append(type.Properties.Count.ToString(CultureInfo.InvariantCulture));
            line.Append(" [");

            int index = 0;

            foreach (ObjectPropertySymbol property in type.Properties.OrderBy(
                    static property => property.Name,
                    StringComparer.Ordinal
                ))
            {
                if (index++ > 0)
                {
                    line.Append(", ");
                }

                line.Append(Quote(property.Name));
                line.Append(' ');
                line.Append(TypeReference(property.Type));
                line.Append(property.IsOptional ? " optional" : " required");
            }

            line.Append(']');
            Line(line.ToString());
        }

        private void WriteFunction(string kind, IrFunction function)
        {
            Line("");
            currentFunctionId = function.Id;
            Line(
                $"{kind} {Quote(function.Id)} {TypeReference(function.ReturnType)} " +
                $"bb{function.EntryBlock.ToString(CultureInfo.InvariantCulture)} " +
                $"slots {function.Slots.Count.ToString(CultureInfo.InvariantCulture)} " +
                $"blocks {function.Blocks.Count.ToString(CultureInfo.InvariantCulture)} {{"
            );

            foreach (IrSlot slot in function.Slots)
            {
                Line(
                    $"  slot %{slot.Id.ToString(CultureInfo.InvariantCulture)} " +
                    $"{GetSlotKind(slot.Kind)} {TypeReference(slot.Type)} " +
                    $"{GetSlotMutability(slot.Mutability)} " +
                    (slot.Name is null ? "none" : Quote(slot.Name))
                );
            }

            foreach (IrBasicBlock block in function.Blocks)
            {
                currentBlockId = block.Id;
                Line(
                    $"  block bb{block.Id.ToString(CultureInfo.InvariantCulture)} " +
                    $"instructions {block.Instructions.Count.ToString(CultureInfo.InvariantCulture)} {{"
                );

                foreach (IrInstruction instruction in block.Instructions)
                {
                    Line($"    ins {WriteInstruction(instruction)}");
                }

                Line($"    term {WriteTerminator(block.Terminator)}");
                Line("  }");
            }

            Line("}");
        }

        private string WriteInstruction(IrInstruction instruction)
        {
            string span = WriteSpan(instruction.Span);

            return instruction switch
            {
                IrInstruction.Constant value =>
                    $"constant {span} {Slot(value.Destination)} {TypeReference(value.Type)} " +
                    WriteConstant(value.Value),
                IrInstruction.Copy value =>
                    $"copy {span} {Slot(value.Destination)} {Slot(value.Source)}",
                IrInstruction.LoadGlobal value =>
                    $"load-global {span} {Slot(value.Destination)} {Quote(value.GlobalId)}",
                IrInstruction.Unary value =>
                    $"unary {span} {Slot(value.Destination)} {GetUnaryOperator(value.Operator)} " +
                    Slot(value.Operand),
                IrInstruction.Binary value =>
                    $"binary {span} {Slot(value.Destination)} {GetBinaryOperator(value.Operator)} " +
                    $"{Slot(value.Left)} {Slot(value.Right)}",
                IrInstruction.Convert value =>
                    $"convert {span} {Slot(value.Destination)} {Slot(value.Source)} " +
                    $"{TypeReference(value.TargetType)} {GetConversionKind(value.Kind)}",
                IrInstruction.Truthiness value =>
                    $"truthiness {span} {Slot(value.Destination)} {Slot(value.Source)}",
                IrInstruction.TypeTest value =>
                    $"type-test {span} {Slot(value.Destination)} {Slot(value.Source)} " +
                    TypeReference(value.TestedType),
                IrInstruction.IsNull value =>
                    $"is-null {span} {Slot(value.Destination)} {Slot(value.Source)}",
                IrInstruction.HasProperty value =>
                    $"has-property {span} {Slot(value.Destination)} {Slot(value.Target)} " +
                    Slot(value.Key),
                IrInstruction.CreateArray value =>
                    $"create-array {span} {Slot(value.Destination)} {TypeReference(value.Type)} " +
                    WriteSlots(value.Elements),
                IrInstruction.CreateObject value =>
                    $"create-object {span} {Slot(value.Destination)} {TypeReference(value.Type)} " +
                    WriteObjectValues(value.Properties),
                IrInstruction.GetProperty value =>
                    $"get-property {span} {Slot(value.Destination)} {Slot(value.Target)} " +
                    $"{Quote(value.Name)} {Boolean(value.IsOptional)} {Boolean(value.IsArrayLength)}",
                IrInstruction.SetProperty value =>
                    $"set-property {span} {Slot(value.Target)} {Quote(value.Name)} {Slot(value.Value)}",
                IrInstruction.RemoveProperty value =>
                    $"remove-property {span} {Slot(value.Target)} {Quote(value.Name)}",
                IrInstruction.GetElement value =>
                    $"get-element {span} {Slot(value.Destination)} {Slot(value.Target)} " +
                    $"{Slot(value.Index)} {Boolean(value.IsObjectAccess)} {Boolean(value.IsOptional)}",
                IrInstruction.SetElement value =>
                    $"set-element {span} {Slot(value.Target)} {Slot(value.Index)} " +
                    $"{Slot(value.Value)} {Boolean(value.IsObjectAccess)}",
                IrInstruction.RemoveElementProperty value =>
                    $"remove-element-property {span} {Slot(value.Target)} {Slot(value.Key)}",
                IrInstruction.ProviderCall value =>
                    $"provider-call {span} {OptionalSlot(value.Destination)} {Quote(value.FunctionId)} " +
                    $"{TypeReference(value.ReturnType)} {WriteSlots(value.Arguments)}",
                IrInstruction.UserCall value =>
                    $"user-call {span} {OptionalSlot(value.Destination)} {Quote(value.FunctionId)} " +
                    $"{TypeReference(value.ReturnType)} {WriteSlots(value.Arguments)}",
                _ => throw Failure($"Instruction '{instruction.GetType().Name}' is not supported."),
            };
        }

        private string WriteTerminator(IrTerminator terminator)
        {
            string span = WriteSpan(terminator.Span);

            return terminator switch
            {
                IrTerminator.Jump value =>
                    $"jump {span} bb{value.TargetBlock.ToString(CultureInfo.InvariantCulture)}",
                IrTerminator.Branch value =>
                    $"branch {span} {Slot(value.Condition)} " +
                    $"bb{value.TrueBlock.ToString(CultureInfo.InvariantCulture)} " +
                    $"bb{value.FalseBlock.ToString(CultureInfo.InvariantCulture)}",
                IrTerminator.Return value =>
                    $"return {span} {OptionalSlot(value.Value)}",
                _ => throw Failure($"Terminator '{terminator.GetType().Name}' is not supported."),
            };
        }

        private string WriteConstant(object? value)
        {
            return value switch
            {
                null => "null",
                bool boolean => $"bool {Boolean(boolean)}",
                long integer => $"int {integer.ToString(CultureInfo.InvariantCulture)}",
                double number => WriteFloat(number),
                string text => $"string {Quote(text)}",
                _ => throw Failure(
                    $"Constant runtime value '{value.GetType().FullName}' is not supported."
                ),
            };
        }

        private static string WriteFloat(double value)
        {
            if (double.IsNaN(value))
            {
                return "float nan";
            }

            if (double.IsPositiveInfinity(value))
            {
                return "float positive-infinity";
            }

            if (double.IsNegativeInfinity(value))
            {
                return "float negative-infinity";
            }

            if (BitConverter.DoubleToInt64Bits(value) == long.MinValue)
            {
                return "float negative-zero";
            }

            return $"float {value.ToString("R", CultureInfo.InvariantCulture)}";
        }

        private static string WriteSlots(IEnumerable<int> slots)
        {
            return $"[{string.Join(", ", slots.Select(Slot))}]";
        }

        private static string WriteObjectValues(
            IEnumerable<IrInstruction.ObjectPropertyValue> properties
        )
        {
            return $"[{string.Join(
                ", ",
                properties.Select(
                    static property => $"{Quote(property.Name)}: {Slot(property.Value)}"
                )
            )}]";
        }

        private string TypeReference(TypeSymbol type)
        {
            if (!typeIds.TryGetValue(type, out int id))
            {
                throw Failure($"Type '{type.DisplayName}' is missing from the type table.");
            }

            return $"t{id.ToString(CultureInfo.InvariantCulture)}";
        }

        private MuIrSerializationException Failure(string message)
        {
            return new MuIrSerializationException(
                $"Function '{currentFunctionId}', block {currentBlockId}: {message}"
            );
        }

        private void Line(string value)
        {
            writer.Write(value);
            writer.Write('\n');
        }

        private static string GetCompilationMode(CompilationMode mode)
        {
            return mode switch
            {
                CompilationMode.Expression => "expression",
                CompilationMode.Program => "program",
                _ => throw new MuIrSerializationException(
                    $"Compilation mode '{mode}' is not supported."
                ),
            };
        }

        private static string GetIntrinsicType(TypeSymbol type)
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

        private static string GetSlotKind(IrSlotKind kind)
        {
            return kind switch
            {
                IrSlotKind.Parameter => "parameter",
                IrSlotKind.Local => "local",
                IrSlotKind.Temporary => "temporary",
                _ => throw new MuIrSerializationException(
                    $"Slot kind '{kind}' is not supported."
                ),
            };
        }

        private static string GetSlotMutability(IrSlotMutability mutability)
        {
            return mutability switch
            {
                IrSlotMutability.Mutable => "mutable",
                IrSlotMutability.ReadOnly => "readonly",
                _ => throw new MuIrSerializationException(
                    $"Slot mutability '{mutability}' is not supported."
                ),
            };
        }

        private static string GetUnaryOperator(IrUnaryOperator value)
        {
            return value switch
            {
                IrUnaryOperator.Identity => "identity",
                IrUnaryOperator.Negate => "negate",
                IrUnaryOperator.LogicalNot => "logical-not",
                IrUnaryOperator.BitwiseNot => "bitwise-not",
                _ => throw new MuIrSerializationException(
                    $"Unary operator '{value}' is not supported."
                ),
            };
        }

        private static string GetBinaryOperator(IrBinaryOperator value)
        {
            return value switch
            {
                IrBinaryOperator.Add => "add",
                IrBinaryOperator.Subtract => "subtract",
                IrBinaryOperator.Multiply => "multiply",
                IrBinaryOperator.Divide => "divide",
                IrBinaryOperator.Remainder => "remainder",
                IrBinaryOperator.LeftShift => "left-shift",
                IrBinaryOperator.RightShift => "right-shift",
                IrBinaryOperator.LessThan => "less-than",
                IrBinaryOperator.LessThanOrEqual => "less-than-or-equal",
                IrBinaryOperator.GreaterThan => "greater-than",
                IrBinaryOperator.GreaterThanOrEqual => "greater-than-or-equal",
                IrBinaryOperator.StructuralEqual => "structural-equal",
                IrBinaryOperator.StructuralNotEqual => "structural-not-equal",
                IrBinaryOperator.IdentityEqual => "identity-equal",
                IrBinaryOperator.IdentityNotEqual => "identity-not-equal",
                IrBinaryOperator.BitwiseAnd => "bitwise-and",
                IrBinaryOperator.BitwiseXor => "bitwise-xor",
                IrBinaryOperator.BitwiseOr => "bitwise-or",
                _ => throw new MuIrSerializationException(
                    $"Binary operator '{value}' is not supported."
                ),
            };
        }

        private static string GetConversionKind(IrConversionKind value)
        {
            return value switch
            {
                IrConversionKind.ValueConversion => "value",
                IrConversionKind.CheckedCast => "checked",
                _ => throw new MuIrSerializationException(
                    $"Conversion kind '{value}' is not supported."
                ),
            };
        }

        private static string Quote(string value)
        {
            StringBuilder builder = new ();
            builder.Append('"');

            foreach (Rune rune in value.EnumerateRunes())
            {
                switch (rune.Value)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;

                    case '\\':
                        builder.Append("\\\\");
                        break;

                    case '\n':
                        builder.Append("\\n");
                        break;

                    case '\r':
                        builder.Append("\\r");
                        break;

                    case '\t':
                        builder.Append("\\t");
                        break;

                    case < 0x20:
                        builder.Append(CultureInfo.InvariantCulture, $"\\u{{{rune.Value:X}}}");
                        break;

                    default:
                        builder.Append(rune.ToString());
                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }

        private static string WriteSpan(TextSpan span)
        {
            return $"{span.Start.ToString(CultureInfo.InvariantCulture)} " +
                span.Length.ToString(CultureInfo.InvariantCulture);
        }

        private static string Slot(int id) => $"%{id.ToString(CultureInfo.InvariantCulture)}";

        private static string OptionalSlot(int? id) => id is null ? "none" : Slot(id.Value);

        private static string Boolean(bool value) => value ? "true" : "false";
    }
}
