namespace MuLang.VisualStudio;

internal interface IMuLangQuickInfoService
{
    Task<MuLangQuickInfoResponse?> GetQuickInfoAsync(
        string uri,
        int line,
        int character,
        CancellationToken cancellationToken
    );
}
