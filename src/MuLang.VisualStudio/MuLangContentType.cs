using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace MuLang.VisualStudio;

internal static class MuLangContentType
{
    public const string Name = "MuLang";

    [Export]
    [Name(Name)]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition ContentTypeDefinition = null!;

    [Export]
    [FileExtension(".mu")]
    [ContentType(Name)]
    internal static FileExtensionToContentTypeDefinition ShortFileExtensionDefinition = null!;

    [Export]
    [FileExtension(".mulang")]
    [ContentType(Name)]
    internal static FileExtensionToContentTypeDefinition LongFileExtensionDefinition = null!;
}
