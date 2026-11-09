using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;
using MuLang.Core.Types;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using static MuLang.IR.Serialization.MuIrMaterializer;

namespace MuLang.IR.Serialization;

internal static class MuIrParser
{
    public static MuIrAnalysisResult Analyze(string text, MuIrReaderOptions options)
    {
        if (text.Length > options.MaximumDocumentLength)
        {
            return new MuIrAnalysisResult(
                new MuIrReadResult(null, [
                    new MuIrDiagnostic(MuIrDiagnosticCodes.LimitExceeded,
                        DiagnosticSeverity.Error, 0, 0, 1, 1,
                        $"The document exceeds the {options.MaximumDocumentLength} character limit."),
                ]),
                null
            );
        }

        try
        {
            DocumentParser parser = new (text, options);
            IrProgram program = parser.Parse();
            return new MuIrAnalysisResult(new MuIrReadResult(program, [ ]), parser.Document);
        }
        catch (ParseException exception)
        {
            return new MuIrAnalysisResult(new MuIrReadResult(null, [ exception.Diagnostic ]), null);
        }
    }

    private sealed class DocumentParser
    {
        private readonly Lexer lexer;
        private readonly MuIrMaterializer materializer;
        private readonly MuIrReaderOptions options;
        private readonly List<TypeDescriptor> typeDescriptors = [ ];
        private readonly List<TypeSymbol> types = [ ];

        private readonly IDictionary<string, string> functionSourceNames =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly IDictionary<string, string> functionQualifiedNames =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly IDictionary<string, TextSpan> functionDeclarationSpans =
            new Dictionary<string, TextSpan>(StringComparer.Ordinal);

        private readonly IDictionary<IrSlotAttributeTarget, string> slotSourceNames =
            new Dictionary<IrSlotAttributeTarget, string>();

        private readonly IDictionary<IrSlotAttributeTarget, TextSpan> slotDeclarationSpans =
            new Dictionary<IrSlotAttributeTarget, TextSpan>();

        private readonly IDictionary<TypeSymbol, IReadOnlyList<string>> typeSourceNames =
            new Dictionary<TypeSymbol, IReadOnlyList<string>>(
                ReferenceEqualityComparer.Instance
            );

        private readonly IList<IrProviderSymbolName> providerGlobalNames = [ ];
        private readonly IList<IrProviderSymbolName> providerFunctionNames = [ ];

        private readonly IDictionary<string, int> attributeCountsByTarget =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private int declaredTypeCount;
        private int formatVersion;
        private Uri? sourceDocument;
        private TextLineMap? sourceLineMap;
        private Token current;
        private Token previous;
        private string? currentFunction;
        private bool instructionOperand;
        private bool writeNextSlot;
        private readonly List<MuIrToken> tokens = [ ];
        private readonly List<MuIrOccurrence> occurrences = [ ];
        private readonly List<MuIrRange> structuralRanges = [ ];
        private readonly Stack<Token> delimiters = new ();

        public MuIrDocument Document => new (
            formatVersion, types, tokens, occurrences, structuralRanges
        );

        public DocumentParser(string text, MuIrReaderOptions options)
        {
            this.options = options;
            materializer = new MuIrMaterializer(typeDescriptors, types, options, Fail);
            lexer = new Lexer(text, options);
            current = lexer.Next();
        }

        public IrProgram Parse()
        {
            Expect("muir", MuIrDiagnosticCodes.InvalidMagic);
            formatVersion = ReadNonNegativeInteger();

            if (formatVersion is not 1 and not 2)
            {
                Fail(
                    MuIrDiagnosticCodes.UnsupportedVersion,
                    $"MuIR format version {formatVersion} is not supported."
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

            materializer.MaterializeTypes();
            IrFunction entry = ParseFunction("entry");
            Expect("users", MuIrDiagnosticCodes.MissingSection);
            int userCount = ReadCount(options.MaximumFunctions - 1, "user function");
            List<IrFunction> users = new (userCount);

            for (int index = 0; index < userCount; index++)
            {
                users.Add(ParseFunction("user"));
            }

            if (formatVersion >= 2)
            {
                ParseAttributes(entry, users);
            }

            Expect("end", MuIrDiagnosticCodes.MissingSection);

            if (current.Kind != TokenKind.End)
            {
                Fail(MuIrDiagnosticCodes.TrailingContent, "The document contains trailing content.");
            }

            try
            {
                IrAttributeCollection attributes = BuildAttributes();

                return materializer.MaterializeProgram(
                    environmentValue, mode, profileValue, entry, users, attributes
                );
            }
            catch (ArgumentException exception)
            {
                Fail(MuIrDiagnosticCodes.MaterializationFailed, exception.Message);
                throw;
            }
        }

        private void ParseAttributes(
            IrFunction entry,
            IReadOnlyList<IrFunction> users
        )
        {
            Expect("attributes", MuIrDiagnosticCodes.MissingSection);
            int count = ReadCount(options.MaximumAttributes, "attribute");
            Expect("{");
            IReadOnlyDictionary<string, IrFunction> functions = users
                .Append(entry)
                .ToDictionary(static function => function.Id, StringComparer.Ordinal);

            for (int index = 0; index < count; index++)
            {
                Expect("attribute", MuIrDiagnosticCodes.InvalidAttribute);
                string targetKind = ReadAtom();
                string targetKey;
                string? functionId = null;
                int? slotId = null;
                TypeSymbol? type = null;

                switch (targetKind)
                {
                    case "program":
                        targetKey = "program";
                        break;

                    case "function":
                        functionId = ReadFunctionTarget(entry, functions);
                        targetKey = $"function:{functionId}";
                        break;

                    case "slot":
                        functionId = ReadFunctionTarget(entry, functions);
                        currentFunction = functionId;
                        slotId = ReadSlot();
                        currentFunction = null;
                        targetKey = $"slot:{functionId}:{slotId.Value}";

                        if (functions[functionId].Slots.All(slot => slot.Id != slotId.Value))
                        {
                            Fail(
                                MuIrDiagnosticCodes.UndefinedReference,
                                $"Slot reference '%{slotId.Value}' is undefined in function '{functionId}'."
                            );
                        }

                        break;

                    case "type":
                        type = ReadTypeReference();
                        targetKey = $"type:{types.IndexOf(type)}";
                        break;

                    default:
                        Fail(
                            MuIrDiagnosticCodes.InvalidAttribute,
                            $"Unknown attribute target kind '{targetKind}'."
                        );
                        throw new UnreachableException();
                }

                int targetCount = attributeCountsByTarget.TryGetValue(
                    targetKey,
                    out int existingCount
                )
                    ? existingCount + 1
                    : 1;

                if (targetCount > options.MaximumAttributesPerTarget)
                {
                    Fail(
                        MuIrDiagnosticCodes.LimitExceeded,
                        $"Attribute target '{targetKey}' exceeds the configured attribute limit."
                    );
                }

                attributeCountsByTarget[targetKey] = targetCount;
                string name = ReadAtom();

                if (!TryParseKnownAttribute(
                        targetKind,
                        functionId,
                        slotId,
                        type,
                        name
                    ))
                {
                    SkipUnknownAttributePayload();
                }
            }

            Expect("}");
        }

        private string ReadFunctionTarget(
            IrFunction entry,
            IReadOnlyDictionary<string, IrFunction> functions
        )
        {
            if (Is("entry"))
            {
                RecordFunction(entry.Id, current);
                Advance();
                return entry.Id;
            }

            Expect("user", MuIrDiagnosticCodes.InvalidAttribute);
            string functionId = ReadFunctionId();

            if (
                functionId == entry.Id ||
                !functions.ContainsKey(functionId)
            )
            {
                Fail(
                    MuIrDiagnosticCodes.UndefinedReference,
                    $"Function attribute target '{functionId}' is undefined."
                );
            }

            return functionId;
        }

        private bool TryParseKnownAttribute(
            string targetKind,
            string? functionId,
            int? slotId,
            TypeSymbol? type,
            string name
        )
        {
            switch (targetKind, name)
            {
                case ("program", "source-document"):
                    ReadSourceDocument(name);
                    return true;

                case ("program", "source-line-map"):
                    ReadSourceLineMap(name);
                    return true;

                case ("program", "provider-global-name"):
                    ReadProviderName(providerGlobalNames, "provider global");
                    return true;

                case ("program", "provider-function-name"):
                    ReadProviderName(providerFunctionNames, "provider function");
                    return true;

                case ("function", "source-name"):
                    ReadStringAttribute(
                        functionSourceNames,
                        functionId!,
                        "function source name"
                    );
                    return true;

                case ("function", "qualified-source-name"):
                    ReadStringAttribute(
                        functionQualifiedNames,
                        functionId!,
                        "qualified function source name"
                    );
                    return true;

                case ("function", "declaration-span"):
                    ReadSpanAttribute(
                        functionDeclarationSpans,
                        functionId!
                    );
                    return true;

                case ("slot", "source-name"):
                    ReadStringAttribute(
                        slotSourceNames,
                        new IrSlotAttributeTarget(functionId!, slotId!.Value),
                        "slot source name"
                    );
                    return true;

                case ("slot", "declaration-span"):
                    ReadSpanAttribute(
                        slotDeclarationSpans,
                        new IrSlotAttributeTarget(functionId!, slotId!.Value)
                    );
                    return true;

                case ("type", "source-type-names"):
                    ReadTypeNames(type!);
                    return true;

                default:
                    if (IsKnownAttributeName(name))
                    {
                        Fail(
                            MuIrDiagnosticCodes.InvalidAttribute,
                            $"Attribute '{name}' is not valid for target '{targetKind}'."
                        );
                    }

                    return false;
            }
        }

        private static bool IsKnownAttributeName(string name)
        {
            return name is
                "source-document" or
                "source-line-map" or
                "provider-global-name" or
                "provider-function-name" or
                "source-name" or
                "qualified-source-name" or
                "declaration-span" or
                "source-type-names";
        }

        private void ReadSourceLineMap(string attributeName)
        {
            if (sourceLineMap is not null)
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    $"Attribute '{attributeName}' is declared more than once."
                );
            }

            Expect("{");
            int length = ReadNonNegativeInteger();
            int lineCount = ReadCount(options.MaximumSourceLines, "source line");
            int[] lineStarts = new int[lineCount];

            for (int index = 0; index < lineStarts.Length; index++)
            {
                lineStarts[index] = ReadNonNegativeInteger();
            }

            Expect("}");

            try
            {
                sourceLineMap = new TextLineMap(length, lineStarts);
            }
            catch (ArgumentException exception)
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    exception.Message
                );
            }
        }

        private void ReadSourceDocument(string attributeName)
        {
            if (sourceDocument is not null)
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    $"Attribute '{attributeName}' is declared more than once."
                );
            }

            Expect("{");
            string value = ReadRequiredString("source document URI");
            Expect("}");

            try
            {
                sourceDocument = new Uri(value, UriKind.Absolute);
            }
            catch (UriFormatException exception)
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    exception.Message
                );
            }
        }

        private void ReadProviderName(
            IList<IrProviderSymbolName> values,
            string description
        )
        {
            if (
                providerGlobalNames.Count + providerFunctionNames.Count >=
                options.MaximumProviderSymbolNameMappings
            )
            {
                Fail(
                    MuIrDiagnosticCodes.LimitExceeded,
                    "Provider-symbol name mappings exceed the configured limit."
                );
            }

            Expect("{");
            string id = ReadRequiredString($"{description} identifier");
            string name = ReadRequiredString($"{description} name");
            Expect("}");

            if (values.Any(value => value.Id == id))
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    $"The {description} identifier '{id}' is mapped more than once."
                );
            }

            values.Add(new IrProviderSymbolName(id, name));
        }

        private void ReadStringAttribute<TKey>(
            IDictionary<TKey, string> values,
            TKey key,
            string description
        )
            where TKey : notnull
        {
            Expect("{");
            string value = ReadRequiredString(description);
            Expect("}");

            if (!values.TryAdd(key, value))
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    $"Attribute '{description}' is declared more than once."
                );
            }
        }

        private void ReadSpanAttribute<TKey>(
            IDictionary<TKey, TextSpan> values,
            TKey key
        )
            where TKey : notnull
        {
            Expect("{");
            TextSpan span = ReadSpan();
            Expect("}");

            if (!values.TryAdd(key, span))
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    "A declaration-span attribute is declared more than once."
                );
            }
        }

        private void ReadTypeNames(TypeSymbol type)
        {
            Expect("{");
            int count = ReadCount(
                options.MaximumSourceTypeNamesPerType,
                "source type name"
            );

            if (count == 0)
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    "A source-type-names attribute cannot be empty."
                );
            }

            Expect("[");
            IList<string> names = [ ];

            for (int index = 0; index < count; index++)
            {
                if (index > 0)
                {
                    Expect(",");
                }

                names.Add(ReadRequiredString("source type name"));
            }

            Expect("]");
            Expect("}");

            if (
                typeSourceNames.ContainsKey(type) ||
                names.Distinct(StringComparer.Ordinal).Count() != names.Count ||
                !names.SequenceEqual(names.Order(StringComparer.Ordinal))
            )
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    "Source type names must be unique and ordinally sorted."
                );
            }

            typeSourceNames.Add(type, names.AsReadOnly());
        }

        private string ReadRequiredString(string description)
        {
            string value = ReadString();

            if (string.IsNullOrWhiteSpace(value))
            {
                Fail(
                    MuIrDiagnosticCodes.InvalidAttribute,
                    $"The {description} cannot be empty or whitespace."
                );
            }

            return value;
        }

        private void SkipUnknownAttributePayload()
        {
            Expect("{");
            int depth = 1;
            int tokens = 0;

            while (depth > 0)
            {
                if (current.Kind == TokenKind.End)
                {
                    Fail(
                        MuIrDiagnosticCodes.InvalidAttribute,
                        "An unknown attribute payload is unterminated."
                    );
                }

                if (Is("{"))
                {
                    depth++;
                }
                else if (Is("}"))
                {
                    depth--;
                }

                Advance();
                tokens++;

                if (tokens > options.MaximumUnknownAttributePayloadTokens)
                {
                    Fail(
                        MuIrDiagnosticCodes.LimitExceeded,
                        "An unknown attribute payload exceeds the configured token limit."
                    );
                }
            }
        }

        private IrAttributeCollection BuildAttributes()
        {
            IrProgramAttributes? program =
                sourceDocument is null &&
                sourceLineMap is null &&
                providerGlobalNames.Count == 0 &&
                providerFunctionNames.Count == 0
                    ? null
                    : new IrProgramAttributes(
                        sourceDocument,
                        providerGlobalNames.AsReadOnly(),
                        providerFunctionNames.AsReadOnly()
                    )
                    {
                        LineMap = sourceLineMap,
                    };
            IEnumerable<KeyValuePair<string, IrFunctionAttributes>> functions =
                functionSourceNames.Keys
                    .Concat(functionQualifiedNames.Keys)
                    .Concat(functionDeclarationSpans.Keys)
                    .Distinct(StringComparer.Ordinal)
                    .Select(
                        id => new KeyValuePair<string, IrFunctionAttributes>(
                            id,
                            new IrFunctionAttributes(
                                GetValue(functionSourceNames, id),
                                GetValue(functionQualifiedNames, id),
                                functionDeclarationSpans.TryGetValue(
                                    id,
                                    out TextSpan span
                                )
                                    ? span
                                    : null
                            )
                        )
                    );
            IEnumerable<KeyValuePair<IrSlotAttributeTarget, IrSlotAttributes>> slots =
                slotSourceNames.Keys
                    .Concat(slotDeclarationSpans.Keys)
                    .Distinct()
                    .Select(
                        target => new KeyValuePair<
                            IrSlotAttributeTarget,
                            IrSlotAttributes
                        >(
                            target,
                            new IrSlotAttributes(
                                GetValue(slotSourceNames, target),
                                slotDeclarationSpans.TryGetValue(
                                    target,
                                    out TextSpan span
                                )
                                    ? span
                                    : null
                            )
                        )
                    );

            return new IrAttributeCollection(
                program,
                functions,
                slots,
                typeSourceNames.Select(
                    static item => new KeyValuePair<
                        TypeSymbol,
                        IrTypeAttributes
                    >(
                        item.Key,
                        new IrTypeAttributes(item.Value)
                    )
                )
            );
        }

        private static TValue? GetValue<TKey, TValue>(
            IDictionary<TKey, TValue> values,
            TKey key
        )
            where TKey : notnull
        {
            return values.TryGetValue(key, out TValue? value)
                ? value
                : default;
        }

        private void ParseType(int expectedId)
        {
            int start = current.Offset;
            Expect("type", MuIrDiagnosticCodes.InvalidType);
            int id = ReadReference('t', MuIrDiagnosticCodes.InvalidType);
            int declaration = Declare(start);

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
            CompleteDeclaration(declaration);
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
                bool isReadOnly = formatVersion >= 2 &&
                    ReadChoice("mutable", "readonly") == "readonly";
                properties.Add(
                    new ObjectPropertyDescriptor(
                        propertyName,
                        propertyType,
                        isOptional,
                        isReadOnly
                    )
                );
            }

            Expect("]");

            return new ObjectTypeDescriptor(isOpen, properties);
        }

        private IrFunction ParseFunction(string expectedKind)
        {
            int start = current.Offset;
            Expect(expectedKind, MuIrDiagnosticCodes.MissingSection);
            string id = ReadFunctionId();
            int declaration = Declare(start);
            currentFunction = id;
            TypeSymbol returnType = ReadTypeReference();
            int entryBlock = ReadReference("bb", MuIrDiagnosticCodes.UndefinedReference);
            Expect("slots");
            int slotCount = ReadCount(options.MaximumSlotsPerFunction, "slot");
            Expect("blocks");
            int blockCount = ReadCount(options.MaximumBlocksPerFunction, "block");
            int lifetimeRegionCount;
            int exceptionRegionCount;

            if (formatVersion >= 2)
            {
                Expect("lifetime-regions");
                lifetimeRegionCount = ReadCount(
                    options.MaximumLifetimeRegionsPerFunction,
                    "lifetime region"
                );
                Expect("exception-regions");
                exceptionRegionCount = ReadCount(
                    options.MaximumExceptionRegionsPerFunction,
                    "exception region"
                );
            }
            else
            {
                lifetimeRegionCount = 1;
                exceptionRegionCount = 0;
            }

            Expect("{");
            List<IrLifetimeRegion> lifetimeRegions = new (lifetimeRegionCount);

            if (formatVersion >= 2)
            {
                for (int index = 0; index < lifetimeRegionCount; index++)
                {
                    int regionStart = current.Offset;
                    Expect("lifetime-region", MuIrDiagnosticCodes.MissingSection);
                    int regionId = ReadReference(
                        "lr",
                        MuIrDiagnosticCodes.UndefinedReference
                    );
                    int regionDeclaration = Declare(regionStart);
                    Expect("parent");
                    int? parentRegion = ReadOptionalLifetimeRegion();
                    Expect("entry");
                    int regionEntryBlock = ReadBlock();
                    lifetimeRegions.Add(
                        new IrLifetimeRegion(
                            regionId,
                            parentRegion,
                            regionEntryBlock
                        )
                    );
                    CompleteDeclaration(regionDeclaration);
                }
            }
            else
            {
                lifetimeRegions.Add(new IrLifetimeRegion(0, null, entryBlock));
            }

            List<IrExceptionRegion> exceptionRegions = new (exceptionRegionCount);

            for (int index = 0; index < exceptionRegionCount; index++)
            {
                int regionStart = current.Offset;
                Expect("exception-region", MuIrDiagnosticCodes.MissingSection);
                int regionId = ReadReference(
                    "er",
                    MuIrDiagnosticCodes.UndefinedReference
                );
                int regionDeclaration = Declare(regionStart);
                Expect("parent");
                int? parentRegion = ReadOptionalExceptionRegion();
                Expect("parent-part");
                IrExceptionRegionPart? parentPart = ReadOptionalExceptionRegionPart();
                Expect("protected");
                IrExceptionProtectedRegion protectedRegion = new (
                    ReadBlock(),
                    ReadBlockList()
                );
                Expect("handler");
                IrExceptionHandler? handler = null;

                if (!Is("none"))
                {
                    int handlerEntry = ReadBlock();
                    int errorSlot = ReadSlot();
                    handler = new IrExceptionHandler(
                        handlerEntry,
                        ReadBlockList(),
                        errorSlot
                    );
                }
                else
                {
                    Advance();
                }

                Expect("cleanup");
                IrExceptionCleanup? cleanup = null;

                if (!Is("none"))
                {
                    cleanup = new IrExceptionCleanup(
                        ReadBlock(),
                        ReadBlockList()
                    );
                }
                else
                {
                    Advance();
                }

                exceptionRegions.Add(
                    new IrExceptionRegion(
                        regionId,
                        parentRegion,
                        parentPart,
                        protectedRegion,
                        handler,
                        cleanup
                    )
                );
                CompleteDeclaration(regionDeclaration);
            }

            List<IrSlot> slots = new (slotCount);

            for (int index = 0; index < slotCount; index++)
            {
                int slotStart = current.Offset;
                Expect("slot", MuIrDiagnosticCodes.MissingSection);
                int slotId = ReadReference('%', MuIrDiagnosticCodes.InvalidInstruction);
                int slotDeclaration = Declare(slotStart);
                IrSlotKind kind = ReadSlotKind();
                TypeSymbol type = ReadTypeReference();
                IrSlotMutability mutability = ReadSlotMutability();
                string? name = formatVersion == 1
                    ? ReadOptionalString()
                    : null;
                int lifetimeRegion = 0;

                if (formatVersion >= 2)
                {
                    if (!Is("region"))
                    {
                        Fail(
                            MuIrDiagnosticCodes.InvalidAttribute,
                            "MuIR 2 slots do not have a positional name operand."
                        );
                    }

                    Expect("region");
                    lifetimeRegion = ReadLifetimeRegion();
                }

                slots.Add(
                    new IrSlot(
                        slotId,
                        kind,
                        type,
                        mutability,
                        lifetimeRegion
                    )
                );

                if (formatVersion == 1 && name is not null)
                {
                    slotSourceNames.Add(
                        new IrSlotAttributeTarget(id, slotId),
                        name
                    );
                }

                CompleteDeclaration(slotDeclaration);
            }

            List<IrBasicBlock> blocks = new (blockCount);

            for (int index = 0; index < blockCount; index++)
            {
                blocks.Add(ParseBlock());
            }

            Expect("}");
            CompleteDeclaration(declaration);
            currentFunction = null;
            return new IrFunction(
                id,
                returnType,
                entryBlock,
                slots,
                blocks,
                lifetimeRegions,
                exceptionRegions
            );
        }

        private IrBasicBlock ParseBlock()
        {
            int start = current.Offset;
            Expect("block", MuIrDiagnosticCodes.MissingSection);
            int id = ReadReference("bb", MuIrDiagnosticCodes.InvalidInstruction);
            int declaration = Declare(start);
            int lifetimeRegion = 0;

            if (formatVersion >= 2)
            {
                Expect("region");
                lifetimeRegion = ReadLifetimeRegion();
            }

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
            CompleteDeclaration(declaration);
            return new IrBasicBlock(id, instructions, terminator, lifetimeRegion);
        }

        private IrInstruction ParseInstruction()
        {
            string opcode = ReadAtom();
            TextSpan span = ReadSpan();
            instructionOperand = true;
            writeNextSlot = opcode is not (
                "set-property" or "remove-property" or
                "set-element" or "remove-element-property"
            );

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
                        ReadFunctionId(),
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
            finally
            {
                instructionOperand = false;
                writeNextSlot = false;
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
            instructionOperand = true;

            try
            {
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
                    "throw" => new IrTerminator.Throw(span, ReadSlot()),
                    "resume" => new IrTerminator.Resume(span),
                    _ => InvalidTerminator(kind),
                };
            }
            finally
            {
                instructionOperand = false;
            }
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

        private IReadOnlyCollection<int> ReadBlockList()
        {
            Expect("[");
            List<int> blocks = [ ];

            while (!Is("]"))
            {
                if (blocks.Count > 0)
                {
                    Expect(",");
                }

                CheckListLimit(blocks.Count);
                blocks.Add(ReadBlock());
            }

            Expect("]");
            return blocks;
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
                "primitive" when formatVersion >= 2 => TypeSymbols.Primitive,
                "error" when formatVersion >= 2 => TypeSymbols.ErrorValue,
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

        private int ReadLifetimeRegion() =>
            ReadReference("lr", MuIrDiagnosticCodes.UndefinedReference);

        private int? ReadOptionalLifetimeRegion()
        {
            if (Is("none"))
            {
                Advance();
                return null;
            }

            return ReadLifetimeRegion();
        }

        private int? ReadOptionalExceptionRegion()
        {
            if (Is("none"))
            {
                Advance();
                return null;
            }

            return ReadReference("er", MuIrDiagnosticCodes.UndefinedReference);
        }

        private IrExceptionRegionPart? ReadOptionalExceptionRegionPart()
        {
            string value = ReadAtom();

            return value switch
            {
                "none" => null,
                "protected" => IrExceptionRegionPart.Protected,
                "handler" => IrExceptionRegionPart.Handler,
                "cleanup" => IrExceptionRegionPart.Cleanup,
                _ => InvalidExceptionRegionPart(value),
            };
        }

        private IrExceptionRegionPart InvalidExceptionRegionPart(string value)
        {
            Fail(
                MuIrDiagnosticCodes.UnexpectedToken,
                $"Unknown exception-region part '{value}'."
            );
            throw new UnreachableException();
        }

        private int? ReadOptionalSlot()
        {
            if (Is("none"))
            {
                writeNextSlot = false;
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
            Token token = current;
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

            RecordReference(prefix == 't' ? MuIrSymbolKind.Type : MuIrSymbolKind.Slot, id, token);
            return id;
        }

        private int ReadReference(string prefix, string code)
        {
            Token token = current;
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

            RecordReference(
                prefix switch
                {
                    "bb" => MuIrSymbolKind.Block,
                    "lr" => MuIrSymbolKind.LifetimeRegion,
                    "er" => MuIrSymbolKind.ExceptionRegion,
                    _ => throw new InvalidOperationException("Unknown MuIR reference kind."),
                },
                id,
                token
            );
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
            previous = current;
            tokens.Add(new MuIrToken(current.Value, new MuIrRange(current.Offset, current.Length), current.Kind == TokenKind.String));

            if (current.Kind == TokenKind.Punctuation)
            {
                if (current.Value is "{" or "[")
                {
                    delimiters.Push(current);
                }
                else if (current.Value is "}" or "]" && delimiters.Count > 0)
                {
                    Token start = delimiters.Pop();
                    structuralRanges.Add(new MuIrRange(start.Offset, current.Offset + current.Length - start.Offset));
                }
            }

            current = lexer.Next();
        }

        private void RecordReference(MuIrSymbolKind kind, int id, Token token)
        {
            MuIrOccurrenceRole role = MuIrOccurrenceRole.Reference;

            if (kind == MuIrSymbolKind.Slot && instructionOperand)
            {
                role = writeNextSlot ? MuIrOccurrenceRole.Write : MuIrOccurrenceRole.Read;
                writeNextSlot = false;
            }

            MuIrRange range = new (token.Offset, token.Length);
            occurrences.Add(new MuIrOccurrence(
                kind,
                id.ToString(CultureInfo.InvariantCulture),
                kind == MuIrSymbolKind.Type ? null : currentFunction,
                role,
                range,
                range
            ));
        }

        private string ReadFunctionId()
        {
            Token token = current;
            string id = ReadString();
            RecordFunction(id, token);
            return id;
        }

        private void RecordFunction(string id, Token token)
        {
            MuIrRange range = new (token.Offset, token.Length);
            occurrences.Add(new MuIrOccurrence(
                MuIrSymbolKind.Function, id, null, MuIrOccurrenceRole.Reference, range, range
            ));
        }

        private int Declare(int start)
        {
            int index = occurrences.Count - 1;
            occurrences[index] = occurrences[index] with
            {
                Role = MuIrOccurrenceRole.Declaration,
                Range = new MuIrRange(start, 0),
            };
            return index;
        }

        private void CompleteDeclaration(int index)
        {
            MuIrOccurrence occurrence = occurrences[index];
            occurrences[index] = occurrence with
            {
                Range = new MuIrRange(
                    occurrence.Range.Offset,
                    previous.Offset + previous.Length - occurrence.Range.Offset
                ),
            };
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
