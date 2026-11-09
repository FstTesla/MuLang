using System.Text.Json;

namespace MuLang.LanguageServer;

internal sealed class WorkspaceConfigurationException : FormatException
{
    public WorkspaceConfigurationException(string message, JsonElement element)
        : base(message)
    {
        Element = element;
    }

    public JsonElement Element { get; }
}
