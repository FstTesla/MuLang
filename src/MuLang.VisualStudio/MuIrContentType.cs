using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.VisualStudio;

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class MuIrContentType
{
    public const string Name = "MuIR";

    [Export]
    [Name(Name)]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition ContentTypeDefinition = null!;

    [Export]
    [FileExtension(".muir")]
    [ContentType(Name)]
    internal static FileExtensionToContentTypeDefinition FileExtensionDefinition = null!;
}
