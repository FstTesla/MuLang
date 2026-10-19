using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using System.IO;

namespace MuLang.VisualStudio;

internal sealed class MuLangQuickInfoSource : IAsyncQuickInfoSource
{
    private readonly IMuLangQuickInfoService quickInfoService;
    private readonly ITextBuffer textBuffer;
    private readonly ITextDocumentFactoryService textDocumentFactoryService;

    public MuLangQuickInfoSource(
        ITextBuffer textBuffer,
        ITextDocumentFactoryService textDocumentFactoryService,
        IMuLangQuickInfoService quickInfoService
    )
    {
        this.textBuffer = textBuffer ??
            throw new ArgumentNullException(nameof(textBuffer));
        this.textDocumentFactoryService = textDocumentFactoryService ??
            throw new ArgumentNullException(
                nameof(textDocumentFactoryService)
            );
        this.quickInfoService = quickInfoService ??
            throw new ArgumentNullException(nameof(quickInfoService));
    }

    public void Dispose() { }

    public async Task<QuickInfoItem?> GetQuickInfoItemAsync(
        IAsyncQuickInfoSession session,
        CancellationToken cancellationToken
    )
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        ITextSnapshot snapshot = textBuffer.CurrentSnapshot;
        SnapshotPoint? triggerPoint = session.GetTriggerPoint(snapshot);

        if (
            triggerPoint is null ||
            !textDocumentFactoryService.TryGetTextDocument(
                textBuffer,
                out ITextDocument textDocument
            ) ||
            string.IsNullOrWhiteSpace(textDocument.FilePath)
        )
        {
            return null;
        }

        ITextSnapshotLine triggerLine =
            triggerPoint.Value.GetContainingLine();
        MuLangQuickInfoResponse? response =
            await quickInfoService.GetQuickInfoAsync(
                new Uri(
                    Path.GetFullPath(textDocument.FilePath)
                ).AbsoluteUri,
                triggerLine.LineNumber,
                triggerPoint.Value.Position - triggerLine.Start.Position,
                cancellationToken
            );

        if (
            response is not { Items.Length: > 0, Range: { Start: { } start, End: { } end } }
        )
        {
            return null;
        }

        SnapshotPoint? startPoint = GetPoint(snapshot, start);
        SnapshotPoint? endPoint = GetPoint(snapshot, end);

        if (
            startPoint is null ||
            endPoint is null ||
            endPoint.Value < startPoint.Value
        )
        {
            return null;
        }

        ITrackingSpan applicableToSpan = snapshot.CreateTrackingSpan(
            Span.FromBounds(
                startPoint.Value.Position,
                endPoint.Value.Position
            ),
            SpanTrackingMode.EdgeInclusive
        );
        IReadOnlyList<object> elements =
        [
            .. response.Items.Select(
                static object (item) => CreateElement(item)
            ),
        ];

        return new QuickInfoItem(
            applicableToSpan,
            new ContainerElement(
                ContainerElementStyle.Stacked,
                elements
            )
        );
    }

    private static ClassifiedTextElement CreateElement(
        MuLangQuickInfoResponse.Item item
    )
    {
        return new ClassifiedTextElement(
            new ClassifiedTextRun(
                ClassifiedTextElement.TextClassificationTypeName,
                $"({item.Category}) ",
                ClassifiedTextRunStyle.Italic
            ),
            new ClassifiedTextRun(
                GetClassification(item.Category),
                item.Content
            )
        );
    }

    private static string GetClassification(string category)
    {
        return category switch
        {
            "variable" => "variable",
            "parameter" => "parameter",
            "function" => "method name",
            "type" => "class name",
            "property" => "property name",
            "literal" => "literal",
            "operator" => "operator",
            _ => ClassifiedTextElement.TextClassificationTypeName,
        };
    }

    private static SnapshotPoint? GetPoint(
        ITextSnapshot snapshot,
        MuLangQuickInfoResponse.Position position
    )
    {
        if (
            position.Line < 0 ||
            position.Line >= snapshot.LineCount
        )
        {
            return null;
        }

        ITextSnapshotLine line = snapshot.GetLineFromLineNumber(
            position.Line
        );

        if (
            position.Character < 0 ||
            position.Character > line.Length
        )
        {
            return null;
        }

        return line.Start + position.Character;
    }
}
