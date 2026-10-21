using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;

namespace MuLang.VisualStudio;

internal sealed class MuLangLanguageClientMiddleLayer
    : ILanguageClientMiddleLayer2<JToken>
{
    private readonly IServiceProvider serviceProvider;

    public MuLangLanguageClientMiddleLayer(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public bool CanHandle(string methodName)
    {
        return methodName == "textDocument/references";
    }

    public Task HandleNotificationAsync(
        string methodName,
        JToken methodParam,
        Func<JToken, Task> sendNotification
    )
    {
        return sendNotification(methodParam);
    }

    public async Task<JToken?> HandleRequestAsync(
        string methodName,
        JToken methodParam,
        Func<JToken, Task<JToken?>> sendRequest
    )
    {
        if (CanHandle(methodName))
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            IReadOnlyCollection<string>? documentUris =
                MuLangSolutionSourceService.GetDocumentUris(serviceProvider);

            if (documentUris is not null)
            {
                JObject parameters = (JObject)methodParam.DeepClone();
                parameters["_muLang_solutionDocumentUris"] =
                    new JArray(documentUris);
                methodParam = parameters;
            }
        }

        return await sendRequest(methodParam);
    }
}
