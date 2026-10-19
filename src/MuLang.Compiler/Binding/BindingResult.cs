using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;
using MuLang.Core.Types;

namespace MuLang.Compiler.Binding;

internal sealed record BindingResult(
    BoundRoot Root,
    DiagnosticCollection Diagnostics,
    CompilationMode CompilationMode,
    LanguageProfileFingerprint LanguageProfileFingerprint,
    IReadOnlyDictionary<SyntaxNode, TypeSymbol> Types,
    IReadOnlyDictionary<SyntaxNode, ObjectPropertySymbol> Properties,
    IReadOnlyDictionary<ObjectPropertySymbol, ObjectTypeSymbol> PropertyOwners,
    IReadOnlySet<ObjectPropertySymbol> WellKnownErrorProperties,
    SourceText Source
);
