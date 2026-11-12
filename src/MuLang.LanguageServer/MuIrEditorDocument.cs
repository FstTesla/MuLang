using MuLang.Compiler;
using MuLang.Compiler.Binding;
using MuLang.Core.Text;
using MuLang.Core.Types;
using MuLang.IR;
using MuLang.IR.Serialization;
using System.Globalization;

namespace MuLang.LanguageServer;

internal sealed class MuIrEditorDocument : EditorDocument
{
    public MuIrEditorDocument(string text, SourceText source)
    {
        Analysis = MuIrReader.Analyze(text);

        if (Analysis.Document is not { } document || Analysis.ReadResult.Program is not { } program)
        {
            Occurrences = [ ];
            Symbols = [ ];
            FoldingRanges = [ ];
            return;
        }

        IReadOnlyDictionary<(MuIrSymbolKind Kind, string Id, string? Function), MuIrOccurrence> declarations =
            document.Occurrences
                .Where(static occurrence => occurrence.Role == MuIrOccurrenceRole.Declaration)
                .GroupBy(static occurrence => (occurrence.Kind, occurrence.Id, occurrence.FunctionId))
                .Where(static group => group.Count() == 1)
                .ToDictionary(static group => group.Key, static group => group.Single());
        IReadOnlyList<IrFunction> functions = [ program.EntryFunction, .. program.UserFunctions ];
        IList<EditorOccurrence> occurrences = new List<EditorOccurrence>();

        foreach (MuIrOccurrence occurrence in document.Occurrences)
        {
            if (!declarations.TryGetValue((occurrence.Kind, occurrence.Id, occurrence.FunctionId), out MuIrOccurrence? declaration))
            {
                continue;
            }

            IrFunction? function = functions.FirstOrDefault(
                function => function.Id == (occurrence.Kind == MuIrSymbolKind.Function ? occurrence.Id : occurrence.FunctionId)
            );
            IrSlot? slot = occurrence.Kind == MuIrSymbolKind.Slot
                ? function?.Slots.FirstOrDefault(slot => slot.Id == ParseId(occurrence.Id))
                : null;
            int kind = occurrence.Kind switch
            {
                MuIrSymbolKind.Type => 0,
                MuIrSymbolKind.Function => 1,
                MuIrSymbolKind.Slot when slot?.Kind == IrSlotKind.Parameter => 2,
                MuIrSymbolKind.Slot => 3,
                _ => 5,
            };
            SemanticClassificationModifiers modifiers = occurrence.Role == MuIrOccurrenceRole.Declaration
                ? SemanticClassificationModifiers.Declaration
                : SemanticClassificationModifiers.None;

            if (slot?.Mutability == IrSlotMutability.ReadOnly)
            {
                modifiers |= SemanticClassificationModifiers.ReadOnly;
            }

            occurrences.Add(
                new EditorOccurrence(
                    (this, occurrence.Kind, occurrence.Id, occurrence.FunctionId),
                    kind,
                    occurrence.Role switch
                    {
                        MuIrOccurrenceRole.Declaration => SemanticOccurrenceRole.Declaration,
                        MuIrOccurrenceRole.Read => SemanticOccurrenceRole.Read,
                        MuIrOccurrenceRole.Write => SemanticOccurrenceRole.Write,
                        _ => SemanticOccurrenceRole.Reference,
                    },
                    modifiers,
                    ConvertRange(source, occurrence.SelectionRange),
                    [ CreateHover(occurrence, document, program, function, slot) ],
                    ConvertRange(source, declaration.SelectionRange),
                    true
                )
            );
        }

        Occurrences = [ .. occurrences.OrderBy(static occurrence => occurrence.Span.Start) ];
        Symbols =
        [
            .. declarations.Values
                .Where(static declaration => declaration.Kind is MuIrSymbolKind.Type or MuIrSymbolKind.Function)
                .OrderBy(static declaration => declaration.SelectionRange.Offset)
                .Select(declaration => CreateSymbol(declaration, declarations.Values, source)),
        ];
        FoldingRanges =
        [
            .. document.StructuralRanges.Select(range => ConvertRange(source, range))
                .Where(range => source.GetPosition(range.Start).Line < source.GetPosition(range.End).Line)
                .Distinct()
                .OrderBy(static range => range.Start),
        ];
    }

    public MuIrAnalysisResult Analysis { get; }
    public override string LanguageId => "muir";
    public override IReadOnlyList<EditorOccurrence> Occurrences { get; }
    public override IReadOnlyList<EditorDocumentSymbol> Symbols { get; }
    public override IReadOnlyList<TextSpan> FoldingRanges { get; }

    public static bool IsMuIr(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out Uri? documentUri) &&
        documentUri.AbsolutePath.EndsWith(".muir", StringComparison.OrdinalIgnoreCase);

    private static int ParseId(string id) => int.Parse(id, CultureInfo.InvariantCulture);

    private static string Identifier(MuIrOccurrence occurrence) => occurrence.Kind switch
    {
        MuIrSymbolKind.Type => $"t{occurrence.Id}",
        MuIrSymbolKind.Function => $"\"{occurrence.Id}\"",
        MuIrSymbolKind.Slot => $"%{occurrence.Id}",
        MuIrSymbolKind.Block => $"bb{occurrence.Id}",
        MuIrSymbolKind.LifetimeRegion => $"lr{occurrence.Id}",
        MuIrSymbolKind.ExceptionRegion => $"er{occurrence.Id}",
        _ => throw new ArgumentOutOfRangeException(nameof(occurrence)),
    };

    private static EditorHoverItem CreateHover(
        MuIrOccurrence occurrence,
        MuIrDocument document,
        IrProgram program,
        IrFunction? function,
        IrSlot? slot
    )
    {
        IList<EditorHoverPart> parts = new List<EditorHoverPart>();
        string category = occurrence.Kind switch
        {
            MuIrSymbolKind.Type => "type",
            MuIrSymbolKind.Function => "function",
            MuIrSymbolKind.Slot when slot?.Kind == IrSlotKind.Parameter => "parameter",
            MuIrSymbolKind.Slot when slot?.Kind == IrSlotKind.Local => "local variable",
            MuIrSymbolKind.Slot when slot?.Kind == IrSlotKind.Temporary => "temporary variable",
            MuIrSymbolKind.Slot => "variable",
            MuIrSymbolKind.Block => "block",
            MuIrSymbolKind.LifetimeRegion => "lifetime region",
            MuIrSymbolKind.ExceptionRegion => "exception region",
            _ => throw new ArgumentOutOfRangeException(nameof(occurrence)),
        };
        SemanticHoverPartKind partKind = occurrence.Kind switch
        {
            MuIrSymbolKind.Type => SemanticHoverPartKind.Type,
            MuIrSymbolKind.Function => SemanticHoverPartKind.Function,
            MuIrSymbolKind.Slot when slot?.Kind == IrSlotKind.Parameter => SemanticHoverPartKind.Parameter,
            _ => SemanticHoverPartKind.Variable,
        };
        parts.Add(new EditorHoverPart(partKind, Identifier(occurrence)));

        switch (occurrence.Kind)
        {
            case MuIrSymbolKind.Type:
            {
                TypeSymbol type = document.Types[ParseId(occurrence.Id)];
                AddType(type);

                if (program.Attributes.TryGetType(type, out IrTypeAttributes? attributes))
                {
                    AddName(string.Join(", ", attributes.SourceNames));
                }

                break;
            }

            case MuIrSymbolKind.Function when function is not null:
            {
                parts.Add(new EditorHoverPart(SemanticHoverPartKind.Punctuation, "("));
                IrSlot[] parameters = [ .. function.Slots.Where(static slot => slot.Kind == IrSlotKind.Parameter) ];

                for (int index = 0; index < parameters.Length; index++)
                {
                    if (index > 0)
                    {
                        parts.Add(new EditorHoverPart(SemanticHoverPartKind.Punctuation, ", "));
                    }

                    IrSlot parameter = parameters[index];
                    parts.Add(new EditorHoverPart(SemanticHoverPartKind.Parameter, $"%{parameter.Id}"));
                    AddType(parameter.Type);

                    if (program.Attributes.TryGetSlot(function.Id, parameter.Id, out IrSlotAttributes? attributes))
                    {
                        AddName(attributes.SourceName);
                    }
                }

                parts.Add(new EditorHoverPart(SemanticHoverPartKind.Punctuation, ")"));
                AddType(function.ReturnType);

                if (program.Attributes.TryGetFunction(function.Id, out IrFunctionAttributes? functionAttributes))
                {
                    AddName(functionAttributes.QualifiedSourceName ?? functionAttributes.SourceName);
                }

                break;
            }

            case MuIrSymbolKind.Slot when slot is not null && function is not null:
            {
                if (slot.Mutability == IrSlotMutability.ReadOnly)
                {
                    parts.Add(new EditorHoverPart(SemanticHoverPartKind.Operator, " readonly"));
                }

                AddType(slot.Type);

                if (program.Attributes.TryGetSlot(function.Id, slot.Id, out IrSlotAttributes? attributes))
                {
                    AddName(attributes.SourceName);
                }

                break;
            }
        }

        return new EditorHoverItem(category, [ .. parts ]);

        void AddType(TypeSymbol type)
        {
            parts.Add(new EditorHoverPart(SemanticHoverPartKind.Punctuation, ": "));

            foreach (SemanticHoverPart part in SymbolDisplay.Type(
                type,
                objectType => $"t{Enumerable.Range(0, document.Types.Count).First(id => ReferenceEquals(document.Types[id], objectType))}"
            ))
            {
                parts.Add(new EditorHoverPart(part.Kind, part.Text));
            }
        }

        void AddName(string? name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                parts.Add(new EditorHoverPart(SemanticHoverPartKind.Text, $" [{name}]"));
            }
        }
    }

    private static EditorDocumentSymbol CreateSymbol(
        MuIrOccurrence declaration,
        IEnumerable<MuIrOccurrence> declarations,
        SourceText source
    ) => new (
        Identifier(declaration),
        declaration.Kind switch
        {
            MuIrSymbolKind.Type => 5,
            MuIrSymbolKind.Function => 12,
            MuIrSymbolKind.Slot => 13,
            _ => 3,
        },
        ConvertRange(source, declaration.Range),
        ConvertRange(source, declaration.SelectionRange),
        declaration.Kind == MuIrSymbolKind.Function
            ?
            [
                .. declarations
                    .Where(child => child.FunctionId == declaration.Id)
                    .OrderBy(static child => child.SelectionRange.Offset)
                    .Select(child => CreateSymbol(child, [ ], source)),
            ]
            : [ ]
    );

    private static TextSpan ConvertRange(SourceText source, MuIrRange range) =>
        TextSpan.FromBounds(
            EditorPositions.GetScalarOffset(source, range.Offset),
            EditorPositions.GetScalarOffset(source, range.Offset + range.Length)
        );
}
