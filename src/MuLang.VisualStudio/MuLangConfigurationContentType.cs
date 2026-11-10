using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.VisualStudio;

[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class MuLangConfigurationContentType
{
    public const string Name = "MuLangConfiguration";

    [Export]
    [Name(Name)]
    [BaseDefinition("JSON")]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition ContentTypeDefinition = null!;

    [Export]
    [FileName("mulang.config.json")]
    [ContentType(Name)]
    internal static FileExtensionToContentTypeDefinition FileNameDefinition = null!;
}
