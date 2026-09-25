using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Environment;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace MuLang.IR.Serialization;

/// <summary>Provides MuIR deserialization.</summary>
public static class MuIrReader
{
    /// <summary>Reads a MuIR document from text.</summary>
    /// <param name="text">The MuIR document.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(string text)
    {
        return Read(text, null);
    }

    /// <summary>Reads a MuIR document from text.</summary>
    /// <param name="text">The MuIR document.</param>
    /// <param name="options">The reader options, or <c>null</c> to use the defaults.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(
        string text,
        MuIrReaderOptions? options
    )
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        MuIrReaderOptions effectiveOptions = options ?? MuIrReaderOptions.Default;

        if (text.Length > effectiveOptions.MaximumDocumentLength)
        {
            return Failure(
                MuIrDiagnosticCodes.LimitExceeded,
                0,
                0,
                1,
                1,
                $"The document exceeds the {effectiveOptions.MaximumDocumentLength} character limit."
            );
        }

        try
        {
            IrProgram program = new DocumentParser(text, effectiveOptions).Parse();
            return new MuIrReadResult(program, [ ]);
        }
        catch (ParseException exception)
        {
            return new MuIrReadResult(null, [ exception.Diagnostic ]);
        }
    }

    /// <summary>Reads a MuIR document from a text reader.</summary>
    /// <param name="reader">The source reader.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(TextReader reader)
    {
        return Read(reader, null);
    }

    /// <summary>Reads a MuIR document from a text reader.</summary>
    /// <param name="reader">The source reader.</param>
    /// <param name="options">The reader options, or <c>null</c> to use the defaults.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(
        TextReader reader,
        MuIrReaderOptions? options
    )
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        MuIrReaderOptions effectiveOptions = options ?? MuIrReaderOptions.Default;
        string text = ReadText(reader, effectiveOptions.MaximumDocumentLength);
        return Read(text, effectiveOptions);
    }

    /// <summary>Reads a UTF-8 MuIR document from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(Stream stream)
    {
        return Read(stream, true, null);
    }

    /// <summary>Reads a UTF-8 MuIR document from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after reading.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(Stream stream, bool leaveOpen)
    {
        return Read(stream, leaveOpen, null);
    }

    /// <summary>Reads a UTF-8 MuIR document from a stream.</summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="leaveOpen">Whether to leave the stream open after reading.</param>
    /// <param name="options">The reader options, or <c>null</c> to use the defaults.</param>
    /// <returns>The read result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream" /> is <c>null</c>.</exception>
    public static MuIrReadResult Read(
        Stream stream,
        bool leaveOpen,
        MuIrReaderOptions? options
    )
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        MuIrReaderOptions effectiveOptions = options ?? MuIrReaderOptions.Default;

        try
        {
            using StreamReader reader = new (
                stream,
                new UTF8Encoding(false, true),
                true,
                1024,
                leaveOpen
            );
            string text = ReadText(reader, effectiveOptions.MaximumDocumentLength);
            return Read(text, effectiveOptions);
        }
        catch (DecoderFallbackException)
        {
            return Failure(
                MuIrDiagnosticCodes.InvalidUtf8,
                0,
                0,
                1,
                1,
                "The stream contains invalid UTF-8."
            );
        }
    }

    private static string ReadText(TextReader reader, int maximumLength)
    {
        StringBuilder builder = new (Math.Min(maximumLength, 4096));
        char[] buffer = new char[4096];

        while (true)
        {
            int read = reader.Read(buffer, 0, buffer.Length);

            if (read == 0)
            {
                return builder.ToString();
            }

            if (builder.Length > maximumLength - read)
            {
                return new string(' ', maximumLength + 1);
            }

            builder.Append(buffer, 0, read);
        }
    }

    private static MuIrReadResult Failure(
        string code,
        int offset,
        int length,
        int line,
        int column,
        string message
    )
    {
        return new MuIrReadResult(
            null,
            [
                new MuIrDiagnostic(
                    code,
                    DiagnosticSeverity.Error,
                    offset,
                    length,
                    line,
                    column,
                    message
                ),
            ]
        );
    }

    private sealed class DocumentParser
    {
        private readonly Lexer lexer;
        private readonly MuIrReaderOptions options;
        private readonly List<TypeDescriptor> typeDescriptors = [ ];
        private readonly List<TypeSymbol> types = [ ];
        private int declaredTypeCount;
        private Token current;

        public DocumentParser(string text, MuIrReaderOptions options)
        {
            this.options = options;
            lexer = new Lexer(text, options);
            current = lexer.Next();
        }

        public IrProgram Parse()
        {
            Expect("muir", MuIrDiagnosticCodes.InvalidMagic);
            int version = ReadNonNegativeInteger();

            if (version != 1)
            {
                Fail(
                    MuIrDiagnosticCodes.UnsupportedVersion,
                    $"MuIR format version {version} is not supported."
                );
            }

            Expect("mode", MuIrDiagnosticCodes.MissingSection);
            CompilationMode mode = ReadCompilationMode();
            Expect("environment", MuIrDiagnosticCodes.MissingSection);
            string environmentValue = ReadString();
            Expect("profile", MuIrDiagnosticCodes.MissingSection);
            string profileValue = ReadString();
            Expect("types", MuIrDiagnosticCodes.MissingSection);
            int typeCount = ReadCount(options.MaximumTypes, "type");
            declaredTypeCount = typeCount;

            for (int index = 0; index < typeCount; index++)
            {
                ParseType(index);
            }

            MaterializeTypes();
            IrFunction entry = ParseFunction("entry");
            Expect("users", MuIrDiagnosticCodes.MissingSection);
            int userCount = ReadCount(options.MaximumFunctions - 1, "user function");
            List<IrFunction> users = new (userCount);

            for (int index = 0; index < userCount; index++)
            {
                users.Add(ParseFunction("user"));
            }

            Expect("end", MuIrDiagnosticCodes.MissingSection);

            if (current.Kind != TokenKind.End)
            {
                Fail(MuIrDiagnosticCodes.TrailingContent, "The document contains trailing content.");
            }

            try
            {
                return new IrProgram(
                    new EnvironmentFingerprint(environmentValue),
                    mode,
                    new LanguageProfileFingerprint(profileValue),
                    entry,
                    users
                );
            }
            catch (ArgumentException exception)
            {
                Fail(MuIrDiagnosticCodes.MaterializationFailed, exception.Message);
                throw;
            }
        }

        private void ParseType(int expectedId)
        {
            Expect("type", MuIrDiagnosticCodes.InvalidType);
            int id = ReadReference('t', MuIrDiagnosticCodes.InvalidType);

            if (id != expectedId)
            {
                Fail(
                    id < expectedId
                        ? MuIrDiagnosticCodes.DuplicateDeclaration
                        : MuIrDiagnosticCodes.UndefinedReference,
                    $"Expected type declaration 't{expectedId}', but found 't{id}'."
                );
            }

            string kind = ReadAtom();

            TypeDescriptor descriptor = kind switch
            {
                "intrinsic" => new IntrinsicTypeDescriptor(ReadIntrinsicType()),
                "nullable" => new NullableTypeDescriptor(ReadTypeReferenceId()),
                "array" => new ArrayTypeDescriptor(
                    ReadChoice("mutable", "readonly") == "readonly",
                    ReadTypeReferenceId()
                ),
                "object" => ReadObjectType(),
                _ => InvalidTypeDescriptor(kind),
            };
            typeDescriptors.Add(descriptor);
        }

        private TypeDescriptor InvalidTypeDescriptor(string kind)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidType,
                $"Unknown type definition kind '{kind}'."
            );
            throw new UnreachableException();
        }

        private ObjectTypeDescriptor ReadObjectType()
        {
            string identity = ReadChoice("named", "anonymous");
            string? id = identity == "named" ? ReadString() : null;
            string name = ReadString();
            bool isOpen = ReadChoice("open", "closed") == "open";
            int propertyCount = ReadCount(options.MaximumListElements, "object property");
            Expect("[");
            List<ObjectPropertyDescriptor> properties = new (propertyCount);

            for (int index = 0; index < propertyCount; index++)
            {
                if (index > 0)
                {
                    Expect(",");
                }

                string propertyName = ReadString();
                int propertyType = ReadTypeReferenceId();
                bool isOptional = ReadChoice("required", "optional") == "optional";
                properties.Add(
                    new ObjectPropertyDescriptor(
                        propertyName,
                        propertyType,
                        isOptional
                    )
                );
            }

            Expect("]");

            if (id is null)
            {
                if (name != "<anonymous>")
                {
                    Fail(
                        MuIrDiagnosticCodes.InvalidType,
                        "An anonymous object type must use the name '<anonymous>'."
                    );
                }

                return new ObjectTypeDescriptor(null, name, isOpen, properties);
            }

            return new ObjectTypeDescriptor(id, name, isOpen, properties);
        }

        private void MaterializeTypes()
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

                    case ObjectTypeDescriptor { Id: { } id } value:
                        references[index] = builder.DeclareNamed(
                            $"t{index}",
                            id,
                            value.Name,
                            value.IsOpen
                        );
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
                            property.IsOptional
                        );
                    }
                }

                IReadOnlyDictionary<ObjectTypeGraphReference, TypeSymbol> result =
                    builder.Build();

                for (int index = 0; index < references.Length; index++)
                {
                    types.Add(result[references[index]!]);
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

        private IrFunction ParseFunction(string expectedKind)
        {
            Expect(expectedKind, MuIrDiagnosticCodes.MissingSection);
            string id = ReadString();
            TypeSymbol returnType = ReadTypeReference();
            int entryBlock = ReadReference("bb", MuIrDiagnosticCodes.UndefinedReference);
            Expect("slots");
            int slotCount = ReadCount(options.MaximumSlotsPerFunction, "slot");
            Expect("blocks");
            int blockCount = ReadCount(options.MaximumBlocksPerFunction, "block");
            Expect("{");
            List<IrSlot> slots = new (slotCount);

            for (int index = 0; index < slotCount; index++)
            {
                Expect("slot", MuIrDiagnosticCodes.MissingSection);
                int slotId = ReadReference('%', MuIrDiagnosticCodes.InvalidInstruction);
                IrSlotKind kind = ReadSlotKind();
                TypeSymbol type = ReadTypeReference();
                IrSlotMutability mutability = ReadSlotMutability();
                string? name = ReadOptionalString();
                slots.Add(new IrSlot(slotId, kind, type, name, mutability));
            }

            List<IrBasicBlock> blocks = new (blockCount);

            for (int index = 0; index < blockCount; index++)
            {
                blocks.Add(ParseBlock());
            }

            Expect("}");
            return new IrFunction(id, returnType, entryBlock, slots, blocks);
        }

        private IrBasicBlock ParseBlock()
        {
            Expect("block", MuIrDiagnosticCodes.MissingSection);
            int id = ReadReference("bb", MuIrDiagnosticCodes.InvalidInstruction);
            Expect("instructions");
            int instructionCount = ReadCount(
                options.MaximumInstructionsPerBlock,
                "instruction"
            );
            Expect("{");
            List<IrInstruction> instructions = new (instructionCount);

            for (int index = 0; index < instructionCount; index++)
            {
                Expect("ins", MuIrDiagnosticCodes.InvalidInstruction);
                instructions.Add(ParseInstruction());
            }

            Expect("term", MuIrDiagnosticCodes.InvalidTerminator);
            IrTerminator terminator = ParseTerminator();
            Expect("}");
            return new IrBasicBlock(id, instructions, terminator);
        }

        private IrInstruction ParseInstruction()
        {
            string opcode = ReadAtom();
            TextSpan span = ReadSpan();

            try
            {
                return opcode switch
                {
                    "constant" => new IrInstruction.Constant(
                        span,
                        ReadSlot(),
                        ReadTypeReference(),
                        ReadConstant()
                    ),
                    "copy" => new IrInstruction.Copy(span, ReadSlot(), ReadSlot()),
                    "load-global" => new IrInstruction.LoadGlobal(
                        span,
                        ReadSlot(),
                        ReadString()
                    ),
                    "unary" => new IrInstruction.Unary(
                        span,
                        ReadSlot(),
                        ReadUnaryOperator(),
                        ReadSlot()
                    ),
                    "binary" => new IrInstruction.Binary(
                        span,
                        ReadSlot(),
                        ReadBinaryOperator(),
                        ReadSlot(),
                        ReadSlot()
                    ),
                    "convert" => new IrInstruction.Convert(
                        span,
                        ReadSlot(),
                        ReadSlot(),
                        ReadTypeReference(),
                        ReadConversionKind()
                    ),
                    "truthiness" => new IrInstruction.Truthiness(
                        span,
                        ReadSlot(),
                        ReadSlot()
                    ),
                    "type-test" => new IrInstruction.TypeTest(
                        span,
                        ReadSlot(),
                        ReadSlot(),
                        ReadTypeReference()
                    ),
                    "is-null" => new IrInstruction.IsNull(
                        span,
                        ReadSlot(),
                        ReadSlot()
                    ),
                    "has-property" => new IrInstruction.HasProperty(
                        span,
                        ReadSlot(),
                        ReadSlot(),
                        ReadSlot()
                    ),
                    "create-array" => new IrInstruction.CreateArray(
                        span,
                        ReadSlot(),
                        RequireType<ArrayTypeSymbol>(),
                        ReadSlotList()
                    ),
                    "create-object" => new IrInstruction.CreateObject(
                        span,
                        ReadSlot(),
                        RequireType<ObjectTypeSymbol>(),
                        ReadObjectValueList()
                    ),
                    "get-property" => new IrInstruction.GetProperty(
                        span,
                        ReadSlot(),
                        ReadSlot(),
                        ReadString(),
                        ReadBoolean(),
                        ReadBoolean()
                    ),
                    "set-property" => new IrInstruction.SetProperty(
                        span,
                        ReadSlot(),
                        ReadString(),
                        ReadSlot()
                    ),
                    "remove-property" => new IrInstruction.RemoveProperty(
                        span,
                        ReadSlot(),
                        ReadString()
                    ),
                    "get-element" => new IrInstruction.GetElement(
                        span,
                        ReadSlot(),
                        ReadSlot(),
                        ReadSlot(),
                        ReadBoolean(),
                        ReadBoolean()
                    ),
                    "set-element" => new IrInstruction.SetElement(
                        span,
                        ReadSlot(),
                        ReadSlot(),
                        ReadSlot(),
                        ReadBoolean()
                    ),
                    "remove-element-property" =>
                        new IrInstruction.RemoveElementProperty(
                            span,
                            ReadSlot(),
                            ReadSlot()
                        ),
                    "provider-call" => new IrInstruction.ProviderCall(
                        span,
                        ReadOptionalSlot(),
                        ReadString(),
                        ReadTypeReference(),
                        ReadSlotList()
                    ),
                    "user-call" => new IrInstruction.UserCall(
                        span,
                        ReadOptionalSlot(),
                        ReadString(),
                        ReadTypeReference(),
                        ReadSlotList()
                    ),
                    _ => InvalidInstruction(opcode),
                };
            }
            catch (ArgumentException exception)
            {
                Fail(MuIrDiagnosticCodes.InvalidInstruction, exception.Message);
                throw;
            }
        }

        private IrInstruction InvalidInstruction(string opcode)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidInstruction,
                $"Unknown instruction opcode '{opcode}'."
            );
            throw new UnreachableException();
        }

        private IrTerminator ParseTerminator()
        {
            string kind = ReadAtom();
            TextSpan span = ReadSpan();

            return kind switch
            {
                "jump" => new IrTerminator.Jump(span, ReadBlock()),
                "branch" => new IrTerminator.Branch(
                    span,
                    ReadSlot(),
                    ReadBlock(),
                    ReadBlock()
                ),
                "return" => new IrTerminator.Return(span, ReadOptionalSlot()),
                _ => InvalidTerminator(kind),
            };
        }

        private IrTerminator InvalidTerminator(string kind)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidTerminator,
                $"Unknown terminator '{kind}'."
            );
            throw new UnreachableException();
        }

        private object? ReadConstant()
        {
            string kind = ReadAtom();

            return kind switch
            {
                "null" => null,
                "bool" => ReadBoolean(),
                "int" => ReadInteger(),
                "float" => ReadFloat(),
                "string" => ReadString(),
                _ => InvalidConstant(kind),
            };
        }

        private object InvalidConstant(string kind)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidInstruction,
                $"Unknown constant kind '{kind}'."
            );
            throw new UnreachableException();
        }

        private double ReadFloat()
        {
            string value = ReadAtom();

            return value switch
            {
                "nan" => double.NaN,
                "positive-infinity" => double.PositiveInfinity,
                "negative-infinity" => double.NegativeInfinity,
                "negative-zero" => BitConverter.Int64BitsToDouble(long.MinValue),
                _ when double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number
                ) => number,
                _ => InvalidFloat(value),
            };
        }

        private double InvalidFloat(string value)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidNumber,
                $"'{value}' is not a valid binary64 value."
            );
            throw new UnreachableException();
        }

        private IReadOnlyList<int> ReadSlotList()
        {
            Expect("[");
            List<int> slots = [ ];

            while (!Is("]"))
            {
                if (slots.Count > 0)
                {
                    Expect(",");
                }

                CheckListLimit(slots.Count);
                slots.Add(ReadSlot());
            }

            Expect("]");
            return slots;
        }

        private IReadOnlyCollection<IrInstruction.ObjectPropertyValue>
            ReadObjectValueList()
        {
            Expect("[");
            List<IrInstruction.ObjectPropertyValue> properties = [ ];

            while (!Is("]"))
            {
                if (properties.Count > 0)
                {
                    Expect(",");
                }

                CheckListLimit(properties.Count);
                string name = ReadString();
                Expect(":");
                properties.Add(new IrInstruction.ObjectPropertyValue(name, ReadSlot()));
            }

            Expect("]");
            return properties;
        }

        private void CheckListLimit(int count)
        {
            if (count >= options.MaximumListElements)
            {
                Fail(
                    MuIrDiagnosticCodes.LimitExceeded,
                    "A list exceeds the configured element limit."
                );
            }
        }

        private TextSpan ReadSpan()
        {
            int start = ReadNonNegativeInteger();
            int length = ReadNonNegativeInteger();

            try
            {
                _ = checked(start + length);
                return new TextSpan(start, length);
            }
            catch (OverflowException)
            {
                Fail(MuIrDiagnosticCodes.InvalidSpan, "The source span end overflows.");
                throw;
            }
        }

        private TypeSymbol ReadIntrinsicType()
        {
            string name = ReadAtom();

            return name switch
            {
                "bool" => TypeSymbols.Bool,
                "int" => TypeSymbols.Int,
                "float" => TypeSymbols.Float,
                "number" => TypeSymbols.Number,
                "string" => TypeSymbols.String,
                "unknown" => TypeSymbols.Unknown,
                "object" => TypeSymbols.Object,
                "void" => TypeSymbols.Void,
                "null" => TypeSymbols.Null,
                _ => InvalidIntrinsic(name),
            };
        }

        private TypeSymbol InvalidIntrinsic(string name)
        {
            Fail(MuIrDiagnosticCodes.InvalidType, $"Unknown intrinsic type '{name}'.");
            throw new UnreachableException();
        }

        private TypeSymbol ReadTypeReference()
        {
            int id = ReadTypeReferenceId();

            if ((uint)id >= (uint)types.Count)
            {
                Fail(
                    MuIrDiagnosticCodes.UndefinedReference,
                    $"Type reference 't{id}' is undefined."
                );
            }

            return types[id];
        }

        private int ReadTypeReferenceId()
        {
            int id = ReadReference('t', MuIrDiagnosticCodes.UndefinedReference);

            if ((uint)id >= (uint)declaredTypeCount)
            {
                Fail(
                    MuIrDiagnosticCodes.UndefinedReference,
                    $"Type reference 't{id}' is undefined."
                );
            }

            return id;
        }

        private T RequireType<T>()
            where T : TypeSymbol
        {
            TypeSymbol type = ReadTypeReference();
            T? required = type as T;

            if (required is null)
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidInstruction,
                    $"Type '{type.DisplayName}' is not a {typeof(T).Name}."
                );
            }

            return required;
        }

        private CompilationMode ReadCompilationMode()
        {
            string value = ReadAtom();

            return value switch
            {
                "expression" => CompilationMode.Expression,
                "program" => CompilationMode.Program,
                _ => InvalidCompilationMode(value),
            };
        }

        private CompilationMode InvalidCompilationMode(string value)
        {
            Fail(
                MuIrDiagnosticCodes.UnexpectedToken,
                $"Unknown compilation mode '{value}'."
            );
            throw new UnreachableException();
        }

        private IrSlotKind ReadSlotKind()
        {
            string value = ReadAtom();

            return value switch
            {
                "parameter" => IrSlotKind.Parameter,
                "local" => IrSlotKind.Local,
                "temporary" => IrSlotKind.Temporary,
                _ => InvalidSlotKind(value),
            };
        }

        private IrSlotKind InvalidSlotKind(string value)
        {
            Fail(MuIrDiagnosticCodes.UnexpectedToken, $"Unknown slot kind '{value}'.");
            throw new UnreachableException();
        }

        private IrSlotMutability ReadSlotMutability()
        {
            string value = ReadAtom();

            return value switch
            {
                "mutable" => IrSlotMutability.Mutable,
                "readonly" => IrSlotMutability.ReadOnly,
                _ => InvalidSlotMutability(value),
            };
        }

        private IrSlotMutability InvalidSlotMutability(string value)
        {
            Fail(
                MuIrDiagnosticCodes.UnexpectedToken,
                $"Unknown slot mutability '{value}'."
            );
            throw new UnreachableException();
        }

        private IrUnaryOperator ReadUnaryOperator()
        {
            string value = ReadAtom();

            return value switch
            {
                "identity" => IrUnaryOperator.Identity,
                "negate" => IrUnaryOperator.Negate,
                "logical-not" => IrUnaryOperator.LogicalNot,
                "bitwise-not" => IrUnaryOperator.BitwiseNot,
                _ => InvalidUnaryOperator(value),
            };
        }

        private IrUnaryOperator InvalidUnaryOperator(string value)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidInstruction,
                $"Unknown unary operator '{value}'."
            );
            throw new UnreachableException();
        }

        private IrBinaryOperator ReadBinaryOperator()
        {
            string value = ReadAtom();

            return value switch
            {
                "add" => IrBinaryOperator.Add,
                "subtract" => IrBinaryOperator.Subtract,
                "multiply" => IrBinaryOperator.Multiply,
                "divide" => IrBinaryOperator.Divide,
                "remainder" => IrBinaryOperator.Remainder,
                "left-shift" => IrBinaryOperator.LeftShift,
                "right-shift" => IrBinaryOperator.RightShift,
                "less-than" => IrBinaryOperator.LessThan,
                "less-than-or-equal" => IrBinaryOperator.LessThanOrEqual,
                "greater-than" => IrBinaryOperator.GreaterThan,
                "greater-than-or-equal" => IrBinaryOperator.GreaterThanOrEqual,
                "structural-equal" => IrBinaryOperator.StructuralEqual,
                "structural-not-equal" => IrBinaryOperator.StructuralNotEqual,
                "identity-equal" => IrBinaryOperator.IdentityEqual,
                "identity-not-equal" => IrBinaryOperator.IdentityNotEqual,
                "bitwise-and" => IrBinaryOperator.BitwiseAnd,
                "bitwise-xor" => IrBinaryOperator.BitwiseXor,
                "bitwise-or" => IrBinaryOperator.BitwiseOr,
                _ => InvalidBinaryOperator(value),
            };
        }

        private IrBinaryOperator InvalidBinaryOperator(string value)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidInstruction,
                $"Unknown binary operator '{value}'."
            );
            throw new UnreachableException();
        }

        private IrConversionKind ReadConversionKind()
        {
            string value = ReadAtom();

            return value switch
            {
                "value" => IrConversionKind.ValueConversion,
                "checked" => IrConversionKind.CheckedCast,
                _ => InvalidConversionKind(value),
            };
        }

        private IrConversionKind InvalidConversionKind(string value)
        {
            Fail(
                MuIrDiagnosticCodes.InvalidInstruction,
                $"Unknown conversion kind '{value}'."
            );
            throw new UnreachableException();
        }

        private bool ReadBoolean()
        {
            return ReadChoice("true", "false") == "true";
        }

        private int ReadSlot() =>
            ReadReference('%', MuIrDiagnosticCodes.UndefinedReference);

        private int ReadBlock() =>
            ReadReference("bb", MuIrDiagnosticCodes.UndefinedReference);

        private int? ReadOptionalSlot()
        {
            if (Is("none"))
            {
                Advance();
                return null;
            }

            return ReadSlot();
        }

        private string? ReadOptionalString()
        {
            if (Is("none"))
            {
                Advance();
                return null;
            }

            return ReadString();
        }

        private int ReadCount(int maximum, string description)
        {
            int value = ReadNonNegativeInteger();

            if (value > maximum)
            {
                Fail(
                    MuIrDiagnosticCodes.LimitExceeded,
                    $"The {description} count exceeds the configured limit of {maximum}."
                );
            }

            return value;
        }

        private long ReadInteger()
        {
            string value = ReadAtom();

            if (!long.TryParse(
                    value,
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out long result
                ))
            {
                Fail(
                    IsSignedDecimal(value)
                        ? MuIrDiagnosticCodes.NumericOverflow
                        : MuIrDiagnosticCodes.InvalidNumber,
                    $"'{value}' is not a signed 64-bit integer."
                );
            }

            return result;
        }

        private int ReadNonNegativeInteger()
        {
            string value = ReadAtom();

            if (!int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int result
                ))
            {
                Fail(
                    value.Length > 0 && value.All(static character => char.IsAsciiDigit(character))
                        ? MuIrDiagnosticCodes.NumericOverflow
                        : MuIrDiagnosticCodes.InvalidNumber,
                    $"'{value}' is not a non-negative 32-bit integer."
                );
            }

            return result;
        }

        private static bool IsSignedDecimal(string value)
        {
            int start = value.Length > 0 && value[0] == '-' ? 1 : 0;

            return value.Length > start &&
                value.AsSpan(start).IndexOfAnyExceptInRange('0', '9') < 0;
        }

        private int ReadReference(char prefix, string code)
        {
            string value = ReadAtom();
            int id = -1;

            if (
                value.Length < 2 ||
                value[0] != prefix ||
                !int.TryParse(
                    value.AsSpan(1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out id
                )
            )
            {
                Fail(code, $"'{value}' is not a valid '{prefix}' reference.");
            }

            return id;
        }

        private int ReadReference(string prefix, string code)
        {
            string value = ReadAtom();
            int id = -1;

            if (
                !value.StartsWith(prefix, StringComparison.Ordinal) ||
                value.Length == prefix.Length ||
                !int.TryParse(
                    value.AsSpan(prefix.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out id
                )
            )
            {
                Fail(code, $"'{value}' is not a valid '{prefix}' reference.");
            }

            return id;
        }

        private string ReadChoice(string first, string second)
        {
            string value = ReadAtom();

            if (value != first && value != second)
            {
                Fail(
                    MuIrDiagnosticCodes.UnexpectedToken,
                    $"Expected '{first}' or '{second}', but found '{value}'."
                );
            }

            return value;
        }

        private string ReadAtom()
        {
            if (current.Kind != TokenKind.Atom)
            {
                Fail(
                    MuIrDiagnosticCodes.UnexpectedToken,
                    $"Expected a token, but found '{current.Value}'."
                );
            }

            string value = current.Value;
            Advance();
            return value;
        }

        private string ReadString()
        {
            if (current.Kind != TokenKind.String)
            {
                Fail(
                    MuIrDiagnosticCodes.UnexpectedToken,
                    $"Expected a string, but found '{current.Value}'."
                );
            }

            string value = current.Value;
            Advance();
            return value;
        }

        private void Expect(
            string value,
            string code = MuIrDiagnosticCodes.UnexpectedToken
        )
        {
            if (!Is(value))
            {
                Fail(code, $"Expected '{value}', but found '{current.Value}'.");
            }

            Advance();
        }

        private bool Is(string value) =>
            current.Kind is TokenKind.Atom or TokenKind.Punctuation &&
            current.Value == value;

        private void Advance()
        {
            current = lexer.Next();
        }

        [DoesNotReturn]
        private void Fail(string code, string message)
        {
            throw new ParseException(
                new MuIrDiagnostic(
                    code,
                    DiagnosticSeverity.Error,
                    current.Offset,
                    current.Length,
                    current.Line,
                    current.Column,
                    message
                )
            );
        }
    }

    private abstract record TypeDescriptor;

    private sealed record IntrinsicTypeDescriptor(TypeSymbol Type)
        : TypeDescriptor;

    private sealed record NullableTypeDescriptor(int UnderlyingType)
        : TypeDescriptor;

    private sealed record ArrayTypeDescriptor(bool IsReadOnly, int ElementType)
        : TypeDescriptor;

    private sealed record ObjectTypeDescriptor(
        string? Id,
        string Name,
        bool IsOpen,
        IReadOnlyList<ObjectPropertyDescriptor> Properties
    ) : TypeDescriptor;

    private sealed record ObjectPropertyDescriptor(
        string Name,
        int Type,
        bool IsOptional
    );

    private sealed class Lexer
    {
        private readonly string text;
        private readonly MuIrReaderOptions options;
        private int offset;
        private int line = 1;
        private int column = 1;

        public Lexer(string text, MuIrReaderOptions options)
        {
            this.text = text;
            this.options = options;
        }

        public Token Next()
        {
            SkipTrivia();

            if (offset == text.Length)
            {
                return new Token(TokenKind.End, "", offset, 0, line, column);
            }

            int start = offset;
            int startLine = line;
            int startColumn = column;
            char value = text[offset];

            if (value == '"')
            {
                return ReadString(start, startLine, startColumn);
            }

            if (value is '{' or '}' or '[' or ']' or ',' or ':')
            {
                Advance(value);
                return new Token(
                    TokenKind.Punctuation,
                    value.ToString(),
                    start,
                    1,
                    startLine,
                    startColumn
                );
            }

            while (
                offset < text.Length &&
                !char.IsWhiteSpace(text[offset]) &&
                text[offset] is not ('{' or '}' or '[' or ']' or ',' or ':' or '"' or '#')
            )
            {
                Advance(text[offset]);

                if (offset - start > options.MaximumTokenLength)
                {
                    Throw(
                        MuIrDiagnosticCodes.LimitExceeded,
                        start,
                        offset - start,
                        startLine,
                        startColumn,
                        "A token exceeds the configured length limit."
                    );
                }
            }

            if (offset == start)
            {
                Advance(value);
                Throw(
                    MuIrDiagnosticCodes.InvalidToken,
                    start,
                    1,
                    startLine,
                    startColumn,
                    $"Character '{value}' is not valid in MuIR."
                );
            }

            return new Token(
                TokenKind.Atom,
                text[start..offset],
                start,
                offset - start,
                startLine,
                startColumn
            );
        }

        private Token ReadString(int start, int startLine, int startColumn)
        {
            StringBuilder builder = new ();
            Advance('"');

            while (offset < text.Length)
            {
                char value = text[offset];

                if (value == '"')
                {
                    Advance(value);
                    return new Token(
                        TokenKind.String,
                        builder.ToString(),
                        start,
                        offset - start,
                        startLine,
                        startColumn
                    );
                }

                if (value is '\r' or '\n')
                {
                    Throw(
                        MuIrDiagnosticCodes.UnterminatedString,
                        start,
                        offset - start,
                        startLine,
                        startColumn,
                        "A string literal cannot contain a raw line break."
                    );
                }

                if (value == '\\')
                {
                    ReadEscape(builder, start, startLine, startColumn);
                }
                else if (char.IsHighSurrogate(value))
                {
                    if (
                        offset + 1 >= text.Length ||
                        !char.IsLowSurrogate(text[offset + 1])
                    )
                    {
                        ThrowInvalidScalar(start, startLine, startColumn);
                    }

                    builder.Append(value);
                    Advance(value);
                    builder.Append(text[offset]);
                    Advance(text[offset]);
                }
                else if (char.IsLowSurrogate(value))
                {
                    ThrowInvalidScalar(start, startLine, startColumn);
                }
                else
                {
                    builder.Append(value);
                    Advance(value);
                }

                if (builder.Length > options.MaximumStringLength)
                {
                    Throw(
                        MuIrDiagnosticCodes.LimitExceeded,
                        start,
                        offset - start,
                        startLine,
                        startColumn,
                        "A string exceeds the configured length limit."
                    );
                }
            }

            Throw(
                MuIrDiagnosticCodes.UnterminatedString,
                start,
                offset - start,
                startLine,
                startColumn,
                "The string literal is unterminated."
            );
            throw new UnreachableException();
        }

        private void ReadEscape(
            StringBuilder builder,
            int start,
            int startLine,
            int startColumn
        )
        {
            Advance('\\');

            if (offset == text.Length)
            {
                ThrowInvalidEscape(start, startLine, startColumn);
            }

            char value = text[offset];
            Advance(value);

            switch (value)
            {
                case '"':
                    builder.Append('"');
                    break;

                case '\\':
                    builder.Append('\\');
                    break;

                case 'n':
                    builder.Append('\n');
                    break;

                case 'r':
                    builder.Append('\r');
                    break;

                case 't':
                    builder.Append('\t');
                    break;

                case 'u':
                    ReadUnicodeEscape(builder, start, startLine, startColumn);
                    break;

                default:
                    ThrowInvalidEscape(start, startLine, startColumn);
                    break;
            }
        }

        private void ReadUnicodeEscape(
            StringBuilder builder,
            int start,
            int startLine,
            int startColumn
        )
        {
            if (offset == text.Length || text[offset] != '{')
            {
                ThrowInvalidEscape(start, startLine, startColumn);
            }

            Advance('{');
            int digitsStart = offset;
            int scalar = -1;

            while (
                offset < text.Length &&
                Uri.IsHexDigit(text[offset]) &&
                offset - digitsStart < 6
            )
            {
                Advance(text[offset]);
            }

            if (
                offset == digitsStart ||
                offset == text.Length ||
                text[offset] != '}' ||
                !int.TryParse(
                    text.AsSpan(digitsStart, offset - digitsStart),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out scalar
                ) ||
                !Rune.IsValid(scalar)
            )
            {
                ThrowInvalidEscape(start, startLine, startColumn);
            }

            Advance('}');
            builder.Append(new Rune(scalar).ToString());
        }

        private void SkipTrivia()
        {
            while (offset < text.Length)
            {
                if (char.IsWhiteSpace(text[offset]))
                {
                    Advance(text[offset]);
                    continue;
                }

                if (text[offset] != '#')
                {
                    return;
                }

                while (offset < text.Length && text[offset] is not ('\r' or '\n'))
                {
                    Advance(text[offset]);
                }
            }
        }

        private void Advance(char value)
        {
            offset++;

            if (value == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }

        private static void ThrowInvalidScalar(
            int start,
            int line,
            int column
        )
        {
            Throw(
                MuIrDiagnosticCodes.InvalidEscape,
                start,
                1,
                line,
                column,
                "The string contains an invalid Unicode scalar."
            );
        }

        private static void ThrowInvalidEscape(
            int start,
            int line,
            int column
        )
        {
            Throw(
                MuIrDiagnosticCodes.InvalidEscape,
                start,
                1,
                line,
                column,
                "The string contains an invalid escape sequence."
            );
        }

        [DoesNotReturn]
        private static void Throw(
            string code,
            int offset,
            int length,
            int line,
            int column,
            string message
        )
        {
            throw new ParseException(
                new MuIrDiagnostic(
                    code,
                    DiagnosticSeverity.Error,
                    offset,
                    length,
                    line,
                    column,
                    message
                )
            );
        }
    }

    private sealed class ParseException : Exception
    {
        public ParseException(MuIrDiagnostic diagnostic)
        {
            Diagnostic = diagnostic;
        }

        public MuIrDiagnostic Diagnostic { get; }
    }

    private readonly record struct Token(
        TokenKind Kind,
        string Value,
        int Offset,
        int Length,
        int Line,
        int Column
    );

    private enum TokenKind
    {
        Atom,
        String,
        Punctuation,
        End,
    }
}
