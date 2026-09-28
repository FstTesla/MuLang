namespace MuLang.LanguageServer;

internal sealed record DocumentSnapshot(string Uri, int Version, string Text);
