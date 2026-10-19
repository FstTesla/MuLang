using MuLang.Compiler;
using MuLang.Core.Text;

namespace MuLang.LanguageServer;

internal sealed record SemanticDocumentSnapshot(
    string Uri,
    string Text,
    SourceText Source,
    SemanticDocumentResult SemanticDocument
);
