using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace MuLang.VisualStudio;

[ContentType(MuLangContentType.Name)]
[Export(typeof(IAsyncQuickInfoSourceProvider))]
[Name("MuLang Quick Info")]
internal sealed class MuLangQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
{
    [Import]
    internal IMuLangQuickInfoService QuickInfoService { get; set; } = null!;

    [Import]
    internal ITextDocumentFactoryService TextDocumentFactoryService { get; set; } = null!;

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(
        ITextBuffer textBuffer
    )
    {
        return new MuLangQuickInfoSource(
            textBuffer,
            TextDocumentFactoryService,
            QuickInfoService
        );
    }
}
